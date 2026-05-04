using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class ScoreManagerTests
{
    private readonly ScoreManager _scoreManager = new();

    [Fact]
    public void ComputeResult_scores_mmmPie_counts_with_tied_middle_rank()
    {
        var bob = new Player(0, "Bob");
        var joe = new Player(1, "Joe");
        var sue = new Player(2, "Sue");
        var beth = new Player(3, "Beth");
        AddStashCards(bob, CardName.MmmPie, 4);
        AddStashCards(joe, CardName.MmmPie, 3);
        AddStashCards(sue, CardName.MmmPie, 3);
        AddStashCards(beth, CardName.MmmPie, 1);

        var result = _scoreManager.ComputeResult(new[] { bob, joe, sue, beth });

        AssertScore(result, bob.Index, 6);
        AssertScore(result, joe.Index, 1);
        AssertScore(result, sue.Index, 1);
        AssertScore(result, beth.Index, 1);
        Assert.Equal(bob.Index, result.WinningPlayerIndex);
    }

    [Fact]
    public void ComputeResult_adds_one_point_per_blammo_card()
    {
        var joe = new Player(0, "Joe");
        var beth = new Player(1, "Beth");
        AddStashCards(joe, CardName.Blammo, 5);
        AddStashCards(beth, CardName.Blammo, 2);

        var result = _scoreManager.ComputeResult(new[] { joe, beth });

        AssertScore(result, joe.Index, 5);
        AssertScore(result, beth.Index, 2);
        Assert.Equal(joe.Index, result.WinningPlayerIndex);
    }

    [Fact]
    public void ComputeResult_breaks_score_tie_with_unique_card_type_count()
    {
        var bob = new Player(0, "Bob");
        var joe = new Player(1, "Joe");
        AddStashCards(bob, CardName.Blammo, 4);
        AddStashCards(bob, CardName.Kitteh, 3);
        AddStashCards(joe, CardName.Blammo, 4);
        AddStashCards(joe, CardName.Kitteh, 1);
        AddStashCards(joe, CardName.Doggo, 1);

        var result = _scoreManager.ComputeResult(new[] { bob, joe });

        AssertScore(result, bob.Index, 4);
        AssertScore(result, joe.Index, 4);
        Assert.Equal(joe.Index, result.WinningPlayerIndex);
    }

    [Fact]
    public void ComputeResult_breaks_second_tie_with_total_stashed_cards()
    {
        var bob = new Player(0, "Bob");
        var joe = new Player(1, "Joe");
        AddStashCards(bob, CardName.Blammo, 3);
        AddStashCards(bob, CardName.Kitteh, 4);
        AddStashCards(joe, CardName.Blammo, 3);
        AddStashCards(joe, CardName.Doggo, 2);

        var result = _scoreManager.ComputeResult(new[] { bob, joe });

        AssertScore(result, bob.Index, 3);
        AssertScore(result, joe.Index, 3);
        Assert.Equal(bob.Index, result.WinningPlayerIndex);
    }

    [Fact]
    public void ComputeResult_uses_lowest_player_index_for_full_tie()
    {
        var bob = new Player(0, "Bob");
        var joe = new Player(1, "Joe");

        var result = _scoreManager.ComputeResult(new[] { bob, joe });
        var bobScore = Assert.Single(result.ScoreLines, line => line.PlayerIndex == bob.Index).TotalScore;
        var joeScore = Assert.Single(result.ScoreLines, line => line.PlayerIndex == joe.Index).TotalScore;
        Assert.Equal(bobScore, joeScore);
        Assert.Equal(bob.Index, result.WinningPlayerIndex);
    }

    private static void AddStashCards(Player player, CardName cardName, int count)
    {
        for (var i = 0; i < count; i++)
            player.AddToStash(new Card(cardName), faceUp: true);
    }

    private static void AssertScore(GameEndResult result, int playerIndex, int expectedScore)
    {
        var line = Assert.Single(result.ScoreLines, scoreLine => scoreLine.PlayerIndex == playerIndex);
        Assert.Equal(expectedScore, line.TotalScore);
    }
}
