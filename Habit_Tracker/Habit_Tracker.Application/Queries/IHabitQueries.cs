using Habit_Tracker.Application.DTOs;

namespace Habit_Tracker.Application.Queries;

public interface IHabitQueries
{
    Task<IReadOnlyList<HabitDto>> GetHabitsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    // Returns null if the habit doesn't exist or isn't owned by userId.
    Task<HabitTreeNodeDto?> GetHabitTreeAsync(Guid habitId, Guid userId, CancellationToken cancellationToken = default);
}
