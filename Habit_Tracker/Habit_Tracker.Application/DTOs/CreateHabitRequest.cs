using Habit_Tracker.Domain.Enums;

namespace Habit_Tracker.Application.DTOs;

public class CreateHabitRequest
{
    public required Guid UserId { get; set; }
    public required string Name { get; set; }
    public bool IsCompletable { get; set; }
    public HabitCompletionMethod? CompletionMethod { get; set; }
    public int? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? UnitPlural { get; set; }
    public HabitTargetType? TargetType { get; set; }
    public Guid? ParentHabitId { get; set; }
}
