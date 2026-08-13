using Habit_Tracker.Domain.Enums;

namespace Habit_Tracker.Application.DTOs;

public class HabitDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCompletable { get; set; }
    public HabitCompletionMethod? CompletionMethod { get; set; }
    public int? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? UnitPlural { get; set; }
    public HabitTargetType? TargetType { get; set; }
    public Guid? ParentHabitId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Percentage (0-100) of this habit's tracked entries marked completed; 0 if it has none yet.
    public int CompletionPercentage { get; set; }
}
