using System.Linq;
using TrashAnimal;
using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Covers the <see cref="HandCardView.PlayableAs"/>/<see cref="HandCardView.UnplayableReason"/> ranked-reason
/// contract projected by <see cref="GameSession.GetViewForPlayer"/> (see A2 in
/// <c>.claude/docs/plans/debug-notes-a-correctness-and-blocked-states.md</c>).
/// </summary>
public sealed class GameSessionHandCardPlayabilityTests
{
    private static (Player p0, Player p1, GameSession session) CreateTwoPlayerSession()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, DrawPileMockFactory.CreateEmpty().Object);
        session.ChooseShinyStealVictim = (_, candidates) => candidates[0];
        return (p0, p1, session);
    }

    private static void BustAndAbandon(GameSession session, int playerIndex)
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit, TokenAction.Bandit).Object;
        Assert.True(session.ApplyAction(playerIndex, GameAction.RollDie, die, out var rollErr1, out _), rollErr1);
        Assert.True(session.ApplyAction(playerIndex, GameAction.RollDie, die, out var rollErr2, out _), rollErr2);
        Assert.True(session.PhaseOne.IsBusted);
        Assert.True(session.ApplyAction(playerIndex, GameAction.AbandonBust, die, out var abandonErr, out _), abandonErr);
    }

    [Fact]
    public void NewlyDrawnCard_ReportsDrawnThisTurnReason_ThenPlayableAsOnFollowingTurn()
    {
        var (p0, p1, session) = CreateTwoPlayerSession();
        p1.AddToStash(new Card(CardName.Nanners), faceUp: true);

        var shiny = new Card(CardName.Shiny);
        p0.Hand.Add(shiny, newlyAdded: true);

        var viewThisTurn = session.GetViewForPlayer(0);
        var cardThisTurn = viewThisTurn.HandCards.Single(c => c.CardId == shiny.Id);
        Assert.Null(cardThisTurn.PlayableAs);
        Assert.Equal("Cards drawn during your current turn cannot be played.", cardThisTurn.UnplayableReason);

        // Advance both players through a full turn (bust + AbandonBust) so player 0's BeginTurn clears the flag.
        BustAndAbandon(session, 0);
        Assert.Equal(1, session.CurrentPlayerIndex);
        BustAndAbandon(session, 1);
        Assert.Equal(0, session.CurrentPlayerIndex);
        Assert.Equal(GameState.RollPhase, session.State);

        var viewNextTurn = session.GetViewForPlayer(0);
        var cardNextTurn = viewNextTurn.HandCards.Single(c => c.CardId == shiny.Id);
        Assert.Equal(GameAction.PlayShiny, cardNextTurn.PlayableAs);
        Assert.Null(cardNextTurn.UnplayableReason);
    }

    [Fact]
    public void Ranking_NewlyDrawnAndWrongPhase_ReportsOnlyNewlyDrawnReason()
    {
        var (p0, _, session) = CreateTwoPlayerSession();

        // MmmPie has no RollPhase action at all (it's TokenPhase-only), so absent the NewlyAdded flag this
        // card would report the rank-2 wrong-phase reason. With NewlyAdded set, rank 1 must win instead.
        var mmmPie = new Card(CardName.MmmPie);
        p0.Hand.Add(mmmPie, newlyAdded: true);

        var view = session.GetViewForPlayer(0);
        var cardView = view.HandCards.Single(c => c.CardId == mmmPie.Id);

        Assert.Null(cardView.PlayableAs);
        Assert.Equal("Cards drawn during your current turn cannot be played.", cardView.UnplayableReason);
    }

    [Fact]
    public void WrongPhaseCard_MmmPieDuringRollPhase_NamesCardAndPhase()
    {
        var (p0, _, session) = CreateTwoPlayerSession();

        var mmmPie = new Card(CardName.MmmPie);
        p0.Hand.Add(mmmPie);

        var view = session.GetViewForPlayer(0);
        var cardView = view.HandCards.Single(c => c.CardId == mmmPie.Id);

        Assert.Null(cardView.PlayableAs);
        Assert.Equal("MmmPie cannot be played during the roll phase.", cardView.UnplayableReason);
    }

    [Fact]
    public void ShinyWithAllOpponentStashesEmpty_ReportsTargetUnavailableReason()
    {
        var (p0, p1, session) = CreateTwoPlayerSession();
        p1.StashPile.Clear();

        var shiny = new Card(CardName.Shiny);
        p0.Hand.Add(shiny);

        var view = session.GetViewForPlayer(0);
        var cardView = view.HandCards.Single(c => c.CardId == shiny.Id);

        Assert.Null(cardView.PlayableAs);
        Assert.Equal("No opponent has a card in their stash to steal.", cardView.UnplayableReason);
    }

    [Fact]
    public void NonActivePlayer_WithNoOpenInterruptWindow_SeesEveryCardWithNullPlayableAsAndReason()
    {
        var (p0, p1, session) = CreateTwoPlayerSession();
        Assert.Equal(0, session.CurrentPlayerIndex);

        var shiny = new Card(CardName.Shiny);
        var mmmPie = new Card(CardName.MmmPie);
        var freshlyDrawn = new Card(CardName.Feesh);
        p1.Hand.Add(shiny);
        p1.Hand.Add(mmmPie);
        p1.Hand.Add(freshlyDrawn, newlyAdded: true);

        var view = session.GetViewForPlayer(1);

        Assert.Equal(3, view.HandCards.Count);
        Assert.All(view.HandCards, card =>
        {
            Assert.Null(card.PlayableAs);
            Assert.Null(card.UnplayableReason);
        });
    }
}
