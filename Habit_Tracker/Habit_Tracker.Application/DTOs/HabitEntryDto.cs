namespace Habit_Tracker.Application.DTOs;

public class HabitEntryDto
{
    public Guid Id { get; set; }
    public Guid HabitId { get; set; }
    public DateTimeOffset TrackedAt { get; set; }
    public bool IsCompleted { get; set; }
    public int? Amount { get; set; }
}
