using Habit_Tracker.Application.DTOs;
using Habit_Tracker.Components.Shared;
using Habit_Tracker.Domain.Enums;
using Bunit;

namespace Habit_Tracker.Tests.Components;

public class HabitNodeViewTests : BunitContext
{
    [Fact]
    public void LeafWithNoEntries_ShowsPlaceholderMessage()
    {
        var node = new HabitTreeNodeDto { Id = Guid.NewGuid(), Name = "Drink water", IsCompletable = false };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        Assert.Contains("No entries tracked yet.", cut.Markup);
    }

    [Fact]
    public void LeafWithAmountEntries_ShowsAmountBadgeWithPluralUnit()
    {
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Dune",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            Unit = "page",
            UnitPlural = "pages",
            Entries = [new HabitEntryDto { Id = Guid.NewGuid(), TrackedAt = DateTimeOffset.UtcNow, Amount = 20 }],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        var badge = cut.Find(".badge");
        Assert.Contains("20", badge.TextContent);
        Assert.Contains("pages", badge.TextContent);
    }

    [Fact]
    public void LeafWithAmountOfOne_UsesSingularUnit()
    {
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Dune",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            Unit = "page",
            UnitPlural = "pages",
            Entries = [new HabitEntryDto { Id = Guid.NewGuid(), TrackedAt = DateTimeOffset.UtcNow, Amount = 1 }],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        var badge = cut.Find(".badge");
        Assert.Contains("page", badge.TextContent);
        Assert.DoesNotContain("pages", badge.TextContent);
    }

    [Fact]
    public void LeafWithCheckboxEntries_ShowsCompletedAndNotCompletedBadges()
    {
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Exercise",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            Entries =
            [
                new HabitEntryDto { Id = Guid.NewGuid(), TrackedAt = DateTimeOffset.UtcNow, IsCompleted = true },
                new HabitEntryDto { Id = Guid.NewGuid(), TrackedAt = DateTimeOffset.UtcNow, IsCompleted = false },
            ],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        Assert.Contains("Completed", cut.Markup);
        Assert.Contains("Not completed", cut.Markup);
    }

    [Fact]
    public void NonLeafNode_RendersAccordionForEachSubHabit()
    {
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Reading",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            SubHabits =
            [
                new HabitTreeNodeDto { Id = Guid.NewGuid(), Name = "Dune", IsCompletable = false },
                new HabitTreeNodeDto { Id = Guid.NewGuid(), Name = "Gatsby", IsCompletable = false },
            ],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        Assert.Equal(2, cut.FindAll("details").Count);
        Assert.Contains("Dune", cut.Markup);
        Assert.Contains("Gatsby", cut.Markup);
    }

    [Fact]
    public void SubHabitWithFurtherSubHabits_ShowsViewLinkNotAddEntry()
    {
        var grandchild = new HabitTreeNodeDto { Id = Guid.NewGuid(), Name = "Chapter 1", IsCompletable = false };
        var child = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Dune",
            IsCompletable = false,
            SubHabits = [grandchild],
        };
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Reading",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            SubHabits = [child],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        // Scope to the top-level <details> (representing "child"), not the whole render -
        // its own grandchild is a leaf and legitimately gets its own "Add entry" button deeper
        // in the markup, which isn't what this test is about.
        var childSummary = cut.Find("details > summary");
        var link = childSummary.QuerySelector("a[title='View']");
        Assert.NotNull(link);
        Assert.Contains($"/habits/{child.Id}", link.GetAttribute("href"));
        Assert.Null(childSummary.QuerySelector("button[title='Add entry']"));
    }

    [Fact]
    public void LeafSubHabit_ShowsAddEntryButtonNotViewLink()
    {
        var child = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Dune",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.Total,
            Unit = "page",
            UnitPlural = "pages",
        };
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Reading",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            SubHabits = [child],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        var button = cut.Find("button[title='Add entry']");
        Assert.Contains(child.Id.ToString(), button.GetAttribute("onclick"));
        Assert.Empty(cut.FindAll("a[title='View']"));
    }

    [Fact]
    public void NonCompletableSubHabit_DoesNotRenderProgressRing()
    {
        var completableChild = new HabitTreeNodeDto { Id = Guid.NewGuid(), Name = "Exercise", IsCompletable = true, CompletionMethod = HabitCompletionMethod.SubHabits };
        var nonCompletableChild = new HabitTreeNodeDto { Id = Guid.NewGuid(), Name = "Drink water", IsCompletable = false };
        var node = new HabitTreeNodeDto
        {
            Id = Guid.NewGuid(),
            Name = "Root",
            IsCompletable = true,
            CompletionMethod = HabitCompletionMethod.SubHabits,
            SubHabits = [completableChild, nonCompletableChild],
        };

        var cut = Render<HabitNodeView>(p => p.Add(x => x.Node, node));

        Assert.Single(cut.FindAll(".progress-ring"));
    }
}
