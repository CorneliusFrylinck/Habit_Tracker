using Habit_Tracker.Application.DTOs;
using Habit_Tracker.Domain.Enums;
using Habit_Tracker.Persistence.Commands;
using Habit_Tracker.Persistence.Queries;
using Habit_Tracker.Tests.Infrastructure;

namespace Habit_Tracker.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class HabitQueriesTests(PostgresContainerFixture fixture)
{
    [Fact]
    public async Task GetHabitsForUserAsync_OnlyReturnsHabitsForSpecifiedUser()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userA = await TestDataHelper.CreateTestUserAsync(db);
        var userB = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userA, Name = "A's habit", IsCompletable = false });
        await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userB, Name = "B's habit", IsCompletable = false });

        var habitsForA = await queries.GetHabitsForUserAsync(userA);

        Assert.Single(habitsForA);
        Assert.Equal("A's habit", habitsForA[0].Name);
    }

    [Fact]
    public async Task GetHabitsForUserAsync_NonCompletableWithNoEntries_ZeroPercent()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Drink water", IsCompletable = false });

        var habits = await queries.GetHabitsForUserAsync(userId);

        Assert.Equal(0, Assert.Single(habits).CompletionPercentage);
    }

    [Fact]
    public async Task GetHabitsForUserAsync_SubHabitsMethod_PercentageIsRatioOfCompletedEntries()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Exercise",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
        });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: true, amount: null);
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: true, amount: null);
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: null);

        var habits = await queries.GetHabitsForUserAsync(userId);

        Assert.Equal(67, Assert.Single(habits).CompletionPercentage); // 2/3 rounded
    }

    [Fact]
    public async Task GetHabitsForUserAsync_TotalOnceOff_SumsAmountsCappedAt100()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Dune",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            TargetValue = 100,
            Unit = "page",
            UnitPlural = "pages",
            TargetType = HabitTargetType.OnceOff,
        });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 40);
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 40);
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 40);

        var habits = await queries.GetHabitsForUserAsync(userId);

        Assert.Equal(100, Assert.Single(habits).CompletionPercentage); // 120 summed, capped at 100
    }

    [Fact]
    public async Task GetHabitsForUserAsync_TotalPerEntry_AveragesCappedPerEntryPercentages()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

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

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 8);
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 12);

        var habits = await queries.GetHabitsForUserAsync(userId);

        // (min(100, 80) + min(100, 120)) / 2 = (80 + 100) / 2 = 90
        Assert.Equal(90, Assert.Single(habits).CompletionPercentage);
    }

    [Fact]
    public async Task GetHabitsForUserAsync_TotalPerEntry_IgnoresLegacyEntriesWithNoAmount()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        // Simulate a habit that was reconfigured to Total/PerEntry after already having a
        // pre-existing entry from before that (checkbox-based, no Amount).
        var habit = await commands.CreateHabitAsync(new CreateHabitRequest
        {
            UserId = userId,
            Name = "Exercise",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
        });
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: true, amount: null);

        await commands.UpdateHabitAsync(new UpdateHabitRequest
        {
            HabitId = habit.Id,
            UserId = userId,
            Name = "Exercise",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            TargetValue = 10,
            Unit = "rep",
            UnitPlural = "reps",
            TargetType = HabitTargetType.PerEntry,
        });

        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 5);

        var habits = await queries.GetHabitsForUserAsync(userId);

        // If the legacy no-amount entry were (incorrectly) included via Postgres's LEAST()
        // ignoring NULL, this would come out higher than 50.
        Assert.Equal(50, Assert.Single(habits).CompletionPercentage);
    }

    [Fact]
    public async Task GetHabitTreeAsync_UnknownHabit_ReturnsNull()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var queries = new HabitQueries(dataSource);

        var result = await queries.GetHabitTreeAsync(Guid.NewGuid(), userId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHabitTreeAsync_HabitBelongingToAnotherUser_ReturnsNull()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var ownerId = await TestDataHelper.CreateTestUserAsync(db);
        var otherUserId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = ownerId, Name = "Private", IsCompletable = false });

        var result = await queries.GetHabitTreeAsync(habit.Id, otherUserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHabitTreeAsync_LeafHabit_ReturnsEntriesNotSubHabits()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        var habit = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Drink water", IsCompletable = false });
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 250);
        await commands.AddHabitEntryAsync(habit.Id, userId, isCompleted: false, amount: 500);

        var tree = await queries.GetHabitTreeAsync(habit.Id, userId);

        Assert.NotNull(tree);
        Assert.Empty(tree.SubHabits);
        Assert.Equal(2, tree.Entries.Count);
    }

    [Fact]
    public async Task GetHabitTreeAsync_NonLeafHabit_ReturnsSubHabitsRecursivelyWithEntriesOnLeaves()
    {
        await using var db = fixture.CreateDbContext();
        var dataSource = fixture.CreateDataSource();
        var userId = await TestDataHelper.CreateTestUserAsync(db);
        var commands = new HabitCommands(db);
        var queries = new HabitQueries(dataSource);

        var grandparent = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Reading", IsCompletable = true, CompletionMethod = HabitCompletionMethod.SubHabits });
        var parent = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Dune", IsCompletable = false, ParentHabitId = grandparent.Id });
        var leaf = await commands.CreateHabitAsync(new CreateHabitRequest { UserId = userId, Name = "Chapter 1", IsCompletable = false, ParentHabitId = parent.Id });
        await commands.AddHabitEntryAsync(leaf.Id, userId, isCompleted: false, amount: 20);

        var tree = await queries.GetHabitTreeAsync(grandparent.Id, userId);

        Assert.NotNull(tree);
        Assert.Empty(tree.Entries);
        var parentNode = Assert.Single(tree.SubHabits);
        Assert.Equal("Dune", parentNode.Name);
        Assert.Empty(parentNode.Entries);
        var leafNode = Assert.Single(parentNode.SubHabits);
        Assert.Equal("Chapter 1", leafNode.Name);
        Assert.Empty(leafNode.SubHabits);
        Assert.Single(leafNode.Entries);
    }
}
