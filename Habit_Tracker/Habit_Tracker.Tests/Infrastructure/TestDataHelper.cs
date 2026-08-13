using Habit_Tracker.Persistence;
using Habit_Tracker.Persistence.Identity;

namespace Habit_Tracker.Tests.Infrastructure;

public static class TestDataHelper
{
    // Habits.UserId has a real FK to AspNetUsers, so every test needs an actual user row.
    // Inserted directly rather than through UserManager - password hashing/validation is
    // irrelevant here, only referential integrity is.
    public static async Task<Guid> CreateTestUserAsync(ApplicationDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var email = $"{userId}@test.local";

        dbContext.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("D"),
            ConcurrencyStamp = Guid.NewGuid().ToString("D"),
        });

        await dbContext.SaveChangesAsync();
        return userId;
    }
}
