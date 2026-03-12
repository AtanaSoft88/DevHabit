using DevHabit.Api.Database;
using DevHabit.Api.DTOs.Auth;
using DevHabit.Api.DTOs.Users;
using DevHabit.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DevHabit.Api.Controllers;

// Sharing transactions between multiple DbContexts can be achieved using the same underlying database connection and transaction. In this case, both the ApplicationIdentityDbContext and ApplicationDbContext are using the same PostgreSQL database, so we can share the transaction between them.

[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController(        
    UserManager<IdentityUser> userManager,
    ApplicationIdentityDbContext IdentityDbContext,
    ApplicationDbContext applicationDbContext) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterUserDto registerUserDto) 
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

        // If we reach this point, it means both the IdentityUser and the User entity were created successfully, so we can commit the transaction
        await transaction.CommitAsync();

        // Return the Id of the created User entity as the response to the client for now, we can change this later to return a JWT token 
        return Ok(user.Id);
    }
}
