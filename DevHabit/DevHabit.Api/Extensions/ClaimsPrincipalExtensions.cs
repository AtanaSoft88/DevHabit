using System.Security.Claims;

namespace DevHabit.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    // This extension method allows us to easily retrieve the identity ID (which is typically the user's unique identifier) from a ClaimsPrincipal object. It looks for the claim with the type ClaimTypes.NameIdentifier and returns its value. If the claim is not found, it returns null.
    public static string? GetIdentityId(this ClaimsPrincipal claimsPrincipal)
    {
        string? identityId = claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);

        return identityId;
    }
}
