using System.Security.Claims;
using Habit_Tracker.Application.Commands;
using Microsoft.AspNetCore.Mvc;

namespace Habit_Tracker.Habits;

internal static class HabitEndpointRouteBuilderExtensions
{
    // Delete and "add entry" are plain HTML form posts (see Home.razor) rather than Blazor
    // EditForm submissions - a dynamic per-habit list can't have one statically-named
    // [SupplyParameterFromForm] form per row, but a fixed-route minimal API endpoint has no
    // such constraint.
    public static IEndpointRouteBuilder MapHabitEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/Habits").RequireAuthorization();

        group.MapPost("/{habitId:guid}/Delete", async (
            Guid habitId,
            ClaimsPrincipal user,
            IHabitCommands habitCommands) =>
        {
            await habitCommands.DeleteHabitAsync(habitId, GetUserId(user));
            return Results.LocalRedirect("~/");
        });

        group.MapPost("/{habitId:guid}/Entries", async (
            Guid habitId,
            ClaimsPrincipal user,
            IHabitCommands habitCommands,
            [FromForm] bool isCompleted = false,
            [FromForm] int? amount = null,
            [FromForm] string? returnUrl = null) =>
        {
            await habitCommands.AddHabitEntryAsync(habitId, GetUserId(user), isCompleted, amount);
            return Results.LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "~/" : returnUrl);
        });

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
