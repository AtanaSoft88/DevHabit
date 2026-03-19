using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using DevHabit.Api.Services;
using DevHabit.Api.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace DevHabit.Api.Controllers;

// Sharing transactions between multiple DbContexts can be achieved using the same underlying database connection and transaction. In this case, both the ApplicationIdentityDbContext and ApplicationDbContext are using the same PostgreSQL database, so we can share the transaction between them.

[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController(
    UserManager<IdentityUser> userManager,
    ApplicationIdentityDbContext IdentityDbContext,
    ApplicationDbContext applicationDbContext,
    TokenProvider tokenProvider,
    IOptions<JwtAuthOptions> options) : ControllerBase
{
    private readonly JwtAuthOptions _jwtAuthOptions = options.Value;
    [HttpPost("register")]
    public async Task<ActionResult<AccessTokensDto>> Register(RegisterUserDto registerUserDto)
    {
        // Start a transaction on the IdentityDbContext and share it with the ApplicationDbContext
        using IDbContextTransaction transaction = await IdentityDbContext.Database.BeginTransactionAsync();
        // Share the same database connection and transaction with the ApplicationDbContext
        applicationDbContext.Database.SetDbConnection(IdentityDbContext.Database.GetDbConnection());
        // Use the same transaction for the ApplicationDbContext
        //Now both DbContexts are using the same transaction, so if any operation fails in either context, we can roll back the entire transaction to maintain data integrity.
        await applicationDbContext.Database.UseTransactionAsync(transaction.GetDbTransaction());

        // Create a new Identity User in the IdentityDbContext
        var identityUser = new IdentityUser
        {
            UserName = registerUserDto.Email,
            Email = registerUserDto.Email
        };

        IdentityResult? identityResult = await userManager.CreateAsync(identityUser, registerUserDto.Password);

        if (!identityResult.Succeeded)
        {
            var extensions = new Dictionary<string, object?>
            {
                {
                    "errors",
                    identityResult.Errors.ToDictionary(e => e.Code, e => e.Description)
                }
            };

            return Problem(
                detail: "Unable to register user, please try again",
                statusCode: StatusCodes.Status400BadRequest,
                extensions: extensions);

        }

        // Map from registerUserDto to User entity in the ApplicationDbContext
        User user = registerUserDto.ToEntity();
        // Set the IdentityId to the Id of the created IdentityUser
        user.IdentityId = identityUser.Id;

        // Add the User entity to the ApplicationDbContext and save changes
        applicationDbContext.Users.Add(user);

        await applicationDbContext.SaveChangesAsync();

        // Generate access tokens for the newly registered user
        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email);

        AccessTokensDto accessTokens = tokenProvider.Create(tokenRequest);

        // Create a new RefreshToken entity and associate it with the created IdentityUser
        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            Token = accessTokens.RefreshToken,
            //ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays) // Set the refresh token to expire in 7 days
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(30)
        };

        // Add the refresh token to the IdentityDbContext and save changes
        IdentityDbContext.RefreshTokens.Add(refreshToken);
        await IdentityDbContext.SaveChangesAsync();

        // If we reach this point, it means both the IdentityUser and the User entity were created successfully, so we can commit the transaction
        await transaction.CommitAsync();       

        return Ok(accessTokens);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AccessTokensDto>> Login(LoginUserDto loginUserDto)
    {
        IdentityUser? identityUser = await userManager.FindByEmailAsync(loginUserDto.Email);

        if (identityUser is null || !await userManager.CheckPasswordAsync(identityUser, loginUserDto.Password))
        {
            return Unauthorized();
        }

        var tokenRequest = new TokenRequest(identityUser.Id, identityUser.Email!);
        AccessTokensDto accessTokens = tokenProvider.Create(tokenRequest);

        // Create a new RefreshToken entity and associate it with the created IdentityUser
        var refreshToken = new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = identityUser.Id,
            Token = accessTokens.RefreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays) // Set the refresh token to expire in 7 days
        };

        // Add the refresh token to the IdentityDbContext and save changes
        IdentityDbContext.RefreshTokens.Add(refreshToken);
        await IdentityDbContext.SaveChangesAsync();

        return Ok(accessTokens);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokensDto>> Refresh(RefreshTokenDto refreshTokenDto) 
    {
        RefreshToken? refreshToken = await IdentityDbContext.RefreshTokens
            .Include(rt=> rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == refreshTokenDto.RefreshToken);
        if (refreshToken is null)
        { 
            return Unauthorized();
        }

        if (refreshToken.ExpiresAtUtc < DateTime.UtcNow)
        {
            return Unauthorized();
        }

        var tokenRequest = new TokenRequest(refreshToken.User.Id, refreshToken.User.Email!);
        AccessTokensDto accessTokens = tokenProvider.Create(tokenRequest);

        refreshToken.Token = accessTokens.RefreshToken;
        refreshToken.ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtAuthOptions.RefreshTokenExpirationInDays); // Set the refresh token to expire in 7 days

        await IdentityDbContext.SaveChangesAsync();

        return Ok(accessTokens);
    }
}
