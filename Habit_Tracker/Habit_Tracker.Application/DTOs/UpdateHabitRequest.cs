using Habit_Tracker.Domain.Enums;

namespace Habit_Tracker.Application.DTOs;

public class UpdateHabitRequest
{
    public required Guid HabitId { get; set; }
    public required Guid UserId { get; set; }
    public required string Name { get; set; }
    public bool IsCompletable { get; set; }
    public HabitCompletionMethod? CompletionMethod { get; set; }
    public int? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? UnitPlural { get; set; }
    public HabitTargetType? TargetType { get; set; }
    public List<Guid> RemovedSubHabitIds { get; set; } = [];
}
