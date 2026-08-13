using Habit_Tracker.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Habit_Tracker.Components.Account;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    // Logout is a plain endpoint, not a Razor component: signing out has to happen via a real
    // form POST so the auth cookie change takes effect on the redirect that follows.
    public static IEndpointRouteBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accountGroup = endpoints.MapGroup("/Account");

        accountGroup.MapPost("/Logout", async (
            SignInManager<ApplicationUser> signInManager,
            [FromForm] string returnUrl) =>
        {
            await signInManager.SignOutAsync();
            return TypedResults.LocalRedirect($"~/{returnUrl}");
        });

        return endpoints;
    }
}
