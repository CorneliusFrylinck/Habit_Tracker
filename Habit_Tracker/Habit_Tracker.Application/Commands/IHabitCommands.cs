using Habit_Tracker.Application.DTOs;

namespace Habit_Tracker.Application.Commands;

public interface IHabitCommands
{
    Task<HabitDto> CreateHabitAsync(CreateHabitRequest request, CancellationToken cancellationToken = default);
    Task UpdateHabitAsync(UpdateHabitRequest request, CancellationToken cancellationToken = default);
    Task DeleteHabitAsync(Guid habitId, Guid userId, CancellationToken cancellationToken = default);
    Task AddHabitEntryAsync(Guid habitId, Guid userId, bool isCompleted, int? amount, CancellationToken cancellationToken = default);
}
