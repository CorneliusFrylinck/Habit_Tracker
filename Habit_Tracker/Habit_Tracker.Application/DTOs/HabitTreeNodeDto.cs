using Habit_Tracker.Domain.Enums;

namespace Habit_Tracker.Application.DTOs;

// A habit and its full descendant tree. Leaf nodes (no sub-habits) carry Entries;
// non-leaf nodes carry SubHabits instead - a habit's progress is shown one way or the other.
public class HabitTreeNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsCompletable { get; set; }
    public HabitCompletionMethod? CompletionMethod { get; set; }
    public int? TargetValue { get; set; }
    public string? Unit { get; set; }
    public string? UnitPlural { get; set; }
    public HabitTargetType? TargetType { get; set; }
    public int CompletionPercentage { get; set; }

    // Only set for completable habits with at least one entry: the sum of entry Amounts if any
    // entry has one, else a plain count of entries. Null otherwise.
    public int? TotalCompleted { get; set; }
    public List<HabitTreeNodeDto> SubHabits { get; set; } = [];
    public List<HabitEntryDto> Entries { get; set; } = [];
}
