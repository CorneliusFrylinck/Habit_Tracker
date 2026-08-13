namespace Habit_Tracker.Domain.Entities;

public class HabitEntry
{
    public Guid Id { get; set; }
    public Guid HabitId { get; set; }
    public DateTimeOffset TrackedAt { get; set; }
    public bool IsCompleted { get; set; }

    // Only set when the habit's CompletionMethod is Total; contributes to that habit's
    // CompletionPercentage as a fraction of its TargetValue.
    public int? Amount { get; set; }

    public Habit Habit { get; set; } = null!;
}
