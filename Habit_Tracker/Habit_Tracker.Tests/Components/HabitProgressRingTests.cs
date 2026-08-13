using System.Globalization;
using Habit_Tracker.Components.Shared;
using Bunit;

namespace Habit_Tracker.Tests.Components;

public class HabitProgressRingTests : BunitContext
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void RendersPercentageLabel(int percent)
    {
        var cut = Render<HabitProgressRing>(p => p.Add(x => x.PercentComplete, percent));

        Assert.Contains($"{percent}%", cut.Markup);
    }

    [Theory]
    [InlineData(150, 100)]
    [InlineData(-20, 0)]
    public void ClampsOutOfRangePercentages(int input, int expectedClamped)
    {
        var cut = Render<HabitProgressRing>(p => p.Add(x => x.PercentComplete, input));

        Assert.Contains($"{expectedClamped}%", cut.Markup);
    }

    [Fact]
    public void UsesInvariantDecimalSeparatorRegardlessOfCurrentCulture()
    {
        // Regression test: under a comma-decimal culture (e.g. de-DE), the naive
        // double.ToString() used to render "125,66" for the circumference, which SVG parses
        // as a two-value dasharray and silently breaks the ring (always renders full/solid).
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var cut = Render<HabitProgressRing>(p => p.Add(x => x.PercentComplete, 50));

            var fill = cut.Find(".progress-ring__fill");
            var dashArray = fill.GetAttribute("stroke-dasharray");
            var dashOffset = fill.GetAttribute("stroke-dashoffset");

            Assert.DoesNotContain(",", dashArray);
            Assert.DoesNotContain(",", dashOffset);
            Assert.Contains(".", dashArray!);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void HalfwayDashOffsetIsHalfOfCircumference()
    {
        var cut = Render<HabitProgressRing>(p => p.Add(x => x.PercentComplete, 50));

        var fill = cut.Find(".progress-ring__fill");
        var dashArray = double.Parse(fill.GetAttribute("stroke-dasharray")!, CultureInfo.InvariantCulture);
        var dashOffset = double.Parse(fill.GetAttribute("stroke-dashoffset")!, CultureInfo.InvariantCulture);

        Assert.Equal(dashArray / 2, dashOffset, precision: 6);
    }
}
