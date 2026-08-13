using Habit_Tracker.Application.DTOs;
using Habit_Tracker.Domain.Enums;
using Habit_Tracker.Persistence.Commands;
using Habit_Tracker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Habit_Tracker.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class HabitCommandsTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task CreateHabitAsync_TopLevelNonCompletable_CreatesHabit()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var result = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Drink water",
            IsCompletable = false,
        });

        Assert.Equal("Drink water", result.Name);
        Assert.False(result.IsCompletable);
        Assert.Null(result.CompletionMethod);
        Assert.Null(result.ParentHabitId);
        Assert.Equal(0, result.CompletionPercentage);
    }

    [Fact]
    public async Task CreateHabitAsync_CompletableWithoutMethod_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = userId,
                Name = "Exercise",
                IsCompletable = true,
            }));

        Assert.Equal("Choose how this habit is completed.", ex.Message);
    }

    [Fact]
    public async Task CreateHabitAsync_NonCompletableWithCompletionMethod_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = userId,
                Name = "Drink water",
                IsCompletable = false,
                CompletionMethod = HabitCompletionMethod.SubHabits,
            }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateHabitAsync_TotalWithNonPositiveTarget_Throws(int? target)
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = userId,
                Name = "Read a book",
                IsCompletable = true,
                CompletionMethod = HabitCompletionMethod.Total,
                TargetValue = target,
                Unit = "page",
                UnitPlural = "pages",
                TargetType = HabitTargetType.OnceOff,
            }));
    }

    [Fact]
    public async Task CreateHabitAsync_TotalMissingUnit_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = userId,
                Name = "Read a book",
                IsCompletable = true,
                CompletionMethod = HabitCompletionMethod.Total,
                TargetValue = 100,
                TargetType = HabitTargetType.OnceOff,
            }));
    }

    [Fact]
    public async Task CreateHabitAsync_TotalMissingTargetType_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = userId,
                Name = "Read a book",
                IsCompletable = true,
                CompletionMethod = HabitCompletionMethod.Total,
                TargetValue = 100,
                Unit = "page",
                UnitPlural = "pages",
            }));
    }

    [Fact]
    public async Task CreateHabitAsync_SubHabitsMethodWithTargetFields_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = userId,
                Name = "Reading",
                IsCompletable = true,
                CompletionMethod = HabitCompletionMethod.SubHabits,
                TargetValue = 5,
            }));
    }

    [Fact]
    public async Task CreateHabitAsync_ValidTotal_Succeeds()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var result = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Dune",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            TargetValue = 412,
            Unit = "page",
            UnitPlural = "pages",
            TargetType = HabitTargetType.OnceOff,
        });

        Assert.Equal(HabitCompletionMethod.Total, result.CompletionMethod);
        Assert.Equal(412, result.TargetValue);
        Assert.Equal("page", result.Unit);
        Assert.Equal("pages", result.UnitPlural);
        Assert.Equal(HabitTargetType.OnceOff, result.TargetType);
    }

    [Fact]
    public async Task CreateHabitAsync_ParentFromDifferentUser_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var ownerId = await TestDataHelper.CreateTestUserAsync(db);
        var otherUserId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var otherHabit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = otherUserId,
            Name = "Someone else's habit",
            IsCompletable = false,
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.CreateHabitAsync(new CreateHabitRequest
            {
                UserId = ownerId,
                Name = "Sneaky sub-habit",
                IsCompletable = false,
                ParentHabitId = otherHabit.Id,
            }));
    }

    [Fact]
    public async Task CreateHabitAsync_ValidParentFromSameUser_Succeeds()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var parent = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Reading",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
        });

        var child = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Dune",
            IsCompletable = false,
            ParentHabitId = parent.Id,
        });

        Assert.Equal(parent.Id, child.ParentHabitId);
    }

    [Fact]
    public async Task UpdateHabitAsync_NotFound_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.UpdateHabitAsync(new UpdateHabitRequest
            {
                HabitId = Guid.NewGuid(),
                UserId = userId,
                Name = "Doesn't exist",
                IsCompletable = false,
            }));
    }

    [Fact]
    public async Task UpdateHabitAsync_BelongingToAnotherUser_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var ownerId = await TestDataHelper.CreateTestUserAsync(db);
        var attackerId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = ownerId,
            Name = "Private habit",
            IsCompletable = false,
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.UpdateHabitAsync(new UpdateHabitRequest
            {
                HabitId = habit.Id,
                UserId = attackerId,
                Name = "Hijacked",
                IsCompletable = false,
            }));
    }

    [Fact]
    public async Task UpdateHabitAsync_RenamesAndChangesCompletionSettings()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Exercise",
            IsCompletable = false,
        });

        await commands.UpdateHabitAsync(new UpdateHabitRequest
        {
            HabitId = habit.Id,
            UserId = userId,
            Name = "Exercise daily",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            TargetValue = 10,
            Unit = "rep",
            UnitPlural = "reps",
            TargetType = HabitTargetType.PerEntry,
        });

        var updated = await db.Habits.SingleAsync(h => h.Id == habit.Id);
        Assert.Equal("Exercise daily", updated.Name);
        Assert.True(updated.IsCompletable);
        Assert.Equal(HabitCompletionMethod.Total, updated.CompletionMethod);
        Assert.Equal(10, updated.TargetValue);
        Assert.Equal("rep", updated.Unit);
        Assert.Equal("reps", updated.UnitPlural);
        Assert.Equal(HabitTargetType.PerEntry, updated.TargetType);
    }

    [Fact]
    public async Task UpdateHabitAsync_MakingNonCompletable_ClearsCompletionSettings()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Dune",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            TargetValue = 412,
            Unit = "page",
            UnitPlural = "pages",
            TargetType = HabitTargetType.OnceOff,
        });

        await commands.UpdateHabitAsync(new UpdateHabitRequest
        {
            HabitId = habit.Id,
            UserId = userId,
            Name = "Dune",
            IsCompletable = false,
        });

        var updated = await db.Habits.SingleAsync(h => h.Id == habit.Id);
        Assert.False(updated.IsCompletable);
        Assert.Null(updated.CompletionMethod);
        Assert.Null(updated.TargetValue);
        Assert.Null(updated.Unit);
        Assert.Null(updated.UnitPlural);
        Assert.Null(updated.TargetType);
    }

    [Fact]
    public async Task UpdateHabitAsync_RemovesOnlySpecifiedSubHabits()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var parent = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Reading",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
        });

        var child1 = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Dune",
            IsCompletable = false,
            ParentHabitId = parent.Id,
        });

        var child2 = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Gatsby",
            IsCompletable = false,
            ParentHabitId = parent.Id,
        });

        await commands.UpdateHabitAsync(new UpdateHabitRequest
        {
            HabitId = parent.Id,
            UserId = userId,
            Name = "Reading",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            RemovedSubHabitIds = [child1.Id],
        });

        var updatedChild1 = await db.Habits.SingleAsync(h => h.Id == child1.Id);
        var updatedChild2 = await db.Habits.SingleAsync(h => h.Id == child2.Id);

        Assert.Null(updatedChild1.ParentHabitId);
        Assert.Equal(parent.Id, updatedChild2.ParentHabitId);
    }

    [Fact]
    public async Task UpdateHabitAsync_CannotDetachHabitThatIsNotActuallyItsChild()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var parentA = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "A", IsCompletable = true, CompletionMethod = HabitCompletionMethod.SubHabits });
        var parentB = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "B", IsCompletable = true, CompletionMethod = HabitCompletionMethod.SubHabits });
        var childOfB = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Child of B", IsCompletable = false, ParentHabitId = parentB.Id });

        await commands.UpdateHabitAsync(new UpdateHabitRequest
        {
            HabitId = parentA.Id,
            UserId = userId,
            Name = "A",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            RemovedSubHabitIds = [childOfB.Id],
        });

        var reloaded = await db.Habits.SingleAsync(h => h.Id == childOfB.Id);
        Assert.Equal(parentB.Id, reloaded.ParentHabitId);
    }

    [Fact]
    public async Task DeleteHabitAsync_NotFound_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.DeleteHabitAsync(Guid.NewGuid(), userId));
    }

    [Fact]
    public async Task DeleteHabitAsync_DeletesHabitAndCascadesEntries()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Drink water", IsCompletable = false });
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: null);

        await commands.DeleteHabitAsync(habit.Id, userId);

        Assert.False(await db.Habits.AnyAsync(h => h.Id == habit.Id));
        Assert.False(await db.HabitEntries.AnyAsync(e => e.HabitId == habit.Id));
    }

    [Fact]
    public async Task DeleteHabitAsync_DetachesSubHabitsInsteadOfDeletingThem()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var parent = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Reading", IsCompletable = true, CompletionMethod = HabitCompletionMethod.SubHabits });
        var child = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Dune", IsCompletable = false, ParentHabitId = parent.Id });

        await commands.DeleteHabitAsync(parent.Id, userId);

        Assert.False(await db.Habits.AnyAsync(h => h.Id == parent.Id));
        var reloadedChild = await db.Habits.SingleAsync(h => h.Id == child.Id);
        Assert.Null(reloadedChild.ParentHabitId);
    }

    [Fact]
    public async Task AddHabitEntryAsync_NotFound_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.AddHabitEntryAsync(Guid.NewGuid(), userId, isCompleted: false, amount: null));
    }

    [Fact]
    public async Task AddHabitEntryAsync_NegativeAmount_Throws()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Drink water", IsCompletable = false });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: -1));
    }

    [Fact]
    public async Task AddHabitEntryAsync_NonCompletable_StoresAmountButNeverCompleted()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Drink water", IsCompletable = false });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: true, amount: 500);

        var entry = await db.HabitEntries.SingleAsync(e => e.HabitId == habit.Id);
        Assert.Equal(500, entry.Amount);
        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public async Task AddHabitEntryAsync_NonCompletable_AmountCanBeOmitted()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Drink water", IsCompletable = false });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: null);

        var entry = await db.HabitEntries.SingleAsync(e => e.HabitId == habit.Id);
        Assert.Null(entry.Amount);
    }

    [Fact]
    public async Task AddHabitEntryAsync_TotalMethod_DefaultsAmountToZeroWhenOmitted()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Exercise",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            TargetValue = 10,
            Unit = "rep",
            UnitPlural = "reps",
            TargetType = HabitTargetType.PerEntry,
        });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: true, amount: null);

        var entry = await db.HabitEntries.SingleAsync(e => e.HabitId == habit.Id);
        Assert.Equal(0, entry.Amount);
        Assert.False(entry.IsCompleted);
    }

    [Fact]
    public async Task AddHabitEntryAsync_CompletableViaSubHabits_UsesCheckboxAndIgnoresAmount()
    {
        await using var db = fixture.CreateDbContext();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Exercise",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
        });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: true, amount: 999);

        var entry = await db.HabitEntries.SingleAsync(e => e.HabitId == habit.Id);
        Assert.True(entry.IsCompleted);
        Assert.Null(entry.Amount);
    }
}
