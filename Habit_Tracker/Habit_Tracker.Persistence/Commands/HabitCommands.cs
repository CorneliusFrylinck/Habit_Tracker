using Habit_Tracker.Application.Commands;
using Habit_Tracker.Application.DTOs;
using Habit_Tracker.Domain.Entities;
using Habit_Tracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Habit_Tracker.Persistence.Commands;

public class HabitCommands(ApplicationDbContext dbContext) : IHabitCommands
{
    public async Task<HabitDto> CreateHabitAsync(CreateHabitRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ParentHabitId is { } parentHabitId)
        {
            var parentBelongsToUser = await dbContext.Habits
                .AnyAsync(h => h.Id == parentHabitId && h.UserId == request.UserId, cancellationToken);

            if (!parentBelongsToUser)
            {
                throw new InvalidOperationException("The selected parent habit does not exist.");
            }
        }

        ValidateCompletionSettings(request.IsCompletable, request.CompletionMethod, request.TargetValue, request.Unit, request.UnitPlural, request.TargetType);

        var isTotal = request.CompletionMethod == HabitCompletionMethod.Total;

        var habit = new Habit
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Name = request.Name,
            IsCompletable = request.IsCompletable,
            CompletionMethod = request.IsCompletable ? request.CompletionMethod : null,
            TargetValue = isTotal ? request.TargetValue : null,
            Unit = isTotal ? request.Unit : null,
            UnitPlural = isTotal ? request.UnitPlural : null,
            TargetType = isTotal ? request.TargetType : null,
            ParentHabitId = request.ParentHabitId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.Habits.Add(habit);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new HabitDto
        {
            Id = habit.Id,
            Name = habit.Name,
            IsCompletable = habit.IsCompletable,
            CompletionMethod = habit.CompletionMethod,
            TargetValue = habit.TargetValue,
            Unit = habit.Unit,
            UnitPlural = habit.UnitPlural,
            TargetType = habit.TargetType,
            ParentHabitId = habit.ParentHabitId,
            CreatedAt = habit.CreatedAt,
            CompletionPercentage = 0,
        };
    }

    public async Task UpdateHabitAsync(UpdateHabitRequest request, CancellationToken cancellationToken = default)
    {
        var habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == request.HabitId && h.UserId == request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Habit not found.");

        ValidateCompletionSettings(request.IsCompletable, request.CompletionMethod, request.TargetValue, request.Unit, request.UnitPlural, request.TargetType);

        var isTotal = request.CompletionMethod == HabitCompletionMethod.Total;

        habit.Name = request.Name;
        habit.IsCompletable = request.IsCompletable;
        habit.CompletionMethod = request.IsCompletable ? request.CompletionMethod : null;
        habit.TargetValue = isTotal ? request.TargetValue : null;
        habit.Unit = isTotal ? request.Unit : null;
        habit.UnitPlural = isTotal ? request.UnitPlural : null;
        habit.TargetType = isTotal ? request.TargetType : null;

        if (request.RemovedSubHabitIds.Count > 0)
        {
            var subHabitsToDetach = await dbContext.Habits
                .Where(h => request.RemovedSubHabitIds.Contains(h.Id) && h.ParentHabitId == habit.Id)
                .ToListAsync(cancellationToken);

            foreach (var subHabit in subHabitsToDetach)
            {
                subHabit.ParentHabitId = null;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteHabitAsync(Guid habitId, Guid userId, CancellationToken cancellationToken = default)
    {
        var habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Habit not found.");

        // Detach rather than cascade-delete: removing a parent habit shouldn't silently wipe
        // its sub-habits' own tracked history.
        var subHabits = await dbContext.Habits.Where(h => h.ParentHabitId == habitId).ToListAsync(cancellationToken);
        foreach (var subHabit in subHabits)
        {
            subHabit.ParentHabitId = null;
        }

        dbContext.Habits.Remove(habit);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddHabitEntryAsync(Guid habitId, Guid userId, bool isCompleted, int? amount, CancellationToken cancellationToken = default)
    {
        var habit = await dbContext.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Habit not found.");

        if (amount is < 0)
        {
            throw new InvalidOperationException("Amount cannot be negative.");
        }

        // Total-method habits track progress via Amount, rolled up into CompletionPercentage
        // against TargetValue. Non-completable habits can also record an amount - it's just
        // never counted toward anything. Only "completable via sub-habits" (no total to measure
        // against) falls back to the plain IsCompleted flag.
        var isTotal = habit.CompletionMethod == HabitCompletionMethod.Total;
        var usesCheckbox = habit.IsCompletable && !isTotal;

        dbContext.HabitEntries.Add(new HabitEntry
        {
            Id = Guid.NewGuid(),
            HabitId = habitId,
            TrackedAt = DateTimeOffset.UtcNow,
            IsCompleted = usesCheckbox && isCompleted,
            Amount = usesCheckbox ? null : isTotal ? amount ?? 0 : amount,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Defense in depth: the UI already prevents these combinations, but this is the only
    // place that actually writes a Habit row, so it's the authoritative guard.
    private static void ValidateCompletionSettings(
        bool isCompletable, HabitCompletionMethod? completionMethod, int? targetValue, string? unit, string? unitPlural, HabitTargetType? targetType)
    {
        if (!isCompletable)
        {
            if (completionMethod is not null || targetValue is not null || unit is not null || unitPlural is not null || targetType is not null)
            {
                throw new InvalidOperationException("Completion settings only apply to completable habits.");
            }

            return;
        }

        if (completionMethod is null)
        {
            throw new InvalidOperationException("Choose how this habit is completed.");
        }

        if (completionMethod == HabitCompletionMethod.Total)
        {
            if (targetValue is null or <= 0)
            {
                throw new InvalidOperationException("Enter a target amount greater than zero.");
            }

            if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(unitPlural))
            {
                throw new InvalidOperationException("Enter a unit and its plural form.");
            }

            if (targetType is null)
            {
                throw new InvalidOperationException("Choose whether the target is once-off or per entry.");
            }
        }
        else if (targetValue is not null || unit is not null || unitPlural is not null || targetType is not null)
        {
            throw new InvalidOperationException("Target amount and unit only apply when completed by total.");
        }
    }
}
