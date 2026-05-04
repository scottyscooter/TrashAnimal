using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class TieredCardScoringCalculatorTests
{
    private readonly TieredCardScoringCalculator _calculator = new();

    [Fact]
    public void CalculateTierPoints_assigns_all_three_tiers_when_counts_are_distinct()
    {
        var points = _calculator.CalculateTierPoints(
            new[] { 4, 3, 2, 1 },
            new CardTierPointValues(6, 2, 1));

        Assert.Equal(new[] { 6, 2, 1, 0 }, points);
    }

    [Fact]
    public void CalculateTierPoints_reduces_points_by_one_for_tied_rank()
    {
        var points = _calculator.CalculateTierPoints(
            new[] { 4, 3, 3, 1 },
            new CardTierPointValues(6, 2, 1));

        Assert.Equal(new[] { 6, 1, 1, 1 }, points);
    }

    [Fact]
    public void CalculateTierPoints_clamps_tied_zero_tier_to_zero()
    {
        var points = _calculator.CalculateTierPoints(
            new[] { 4, 2, 2, 1 },
            new CardTierPointValues(3, 0, 0));

        Assert.Equal(new[] { 3, 0, 0, 0 }, points);
    }

    [Fact]
    public void CalculateTierPoints_ignores_fourth_distinct_rank()
    {
        var points = _calculator.CalculateTierPoints(
            new[] { 5, 4, 3, 2 },
            new CardTierPointValues(6, 2, 1));

        Assert.Equal(new[] { 6, 2, 1, 0 }, points);
    }

    [Fact]
    public void CalculateTierPoints_ignores_zero_count_groups()
    {
        var points = _calculator.CalculateTierPoints(
            new[] { 0, 0, 0, 0 },
            new CardTierPointValues(6, 2, 1));

        Assert.Equal(new[] { 0, 0, 0, 0 }, points);
    }
}
