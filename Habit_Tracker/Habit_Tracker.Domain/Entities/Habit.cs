using Habit_Tracker.Domain.Enums;

namespace Habit_Tracker.Domain.Entities;

public class Habit
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCompletable { get; set; }

    // Only set when IsCompletable is true: whether progress comes from sub-habits or a tracked total.
    public HabitCompletionMethod? CompletionMethod { get; set; }

    // Only set when CompletionMethod is Total.
    public int? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? UnitPlural { get; set; }
    public HabitTargetType? TargetType { get; set; }

    public Guid? ParentHabitId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Habit? ParentHabit { get; set; }
    public ICollection<Habit> SubHabits { get; set; } = new List<Habit>();
    public ICollection<HabitEntry> Entries { get; set; } = new List<HabitEntry>();
}
