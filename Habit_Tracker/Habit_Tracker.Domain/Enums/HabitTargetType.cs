namespace Habit_Tracker.Domain.Enums;

// Only meaningful when a habit's CompletionMethod is Total.
public enum HabitTargetType
{
    // TargetValue is a single cumulative goal: entry amounts sum up across the habit's
    // lifetime toward it (e.g. reading a 412-page book).
    OnceOff,

    // TargetValue is the goal for each individual entry (e.g. 10 reps every session);
    // completion is the average of each entry's own progress toward it.
    PerEntry,
}
