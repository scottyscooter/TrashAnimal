using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Covers C3 of the steal-target information-leak fix: the thief's pick-slot order must stay
/// stable across repeated view fetches for the same pending steal attempt, and must shuffle
/// independently once a new attempt begins.
/// </summary>
public sealed class StealPickOrderStabilityTests
{
    private static Player MakeVictimWithHand(int index, int cardCount)
    {
        var victim = new Player(index);
        var names = new[] { CardName.MmmPie, CardName.Feesh, CardName.Shiny, CardName.Nanners, CardName.Doggo, CardName.Kitteh };
        for (var i = 0; i < cardCount; i++)
            victim.Hand.Add(new Card(names[i % names.Length]));
        return victim;
    }

    [Fact]
    public void BuildPhaseView_returns_same_order_across_repeated_calls_for_a_pending_attempt()
    {
        var victim = MakeVictimWithHand(1, 5);
        var players = new List<Player> { new(0), victim };

        var steal = new StealAttempt();
        steal.Begin(thiefIndex: 0, victimIndex: 1, StealTargetZone.Hand);

        var firstView = steal.BuildPhaseView(GameState.AwaitingStealCardPick, viewPlayerIndex: 0, players, new Random(11));
        // Deliberately pass a *different* RNG on the second call: if the order weren't cached,
        // a fresh shuffle from a different seed would almost certainly reorder the slots.
        var secondView = steal.BuildPhaseView(GameState.AwaitingStealCardPick, viewPlayerIndex: 0, players, new Random(999));

        Assert.NotNull(firstView?.ThiefPickSlots);
        Assert.NotNull(secondView?.ThiefPickSlots);
        Assert.Equal(
            firstView!.ThiefPickSlots!.Select(s => s.CardId),
            secondView!.ThiefPickSlots!.Select(s => s.CardId));
    }

    [Fact]
    public void A_subsequent_attempt_shuffles_independently_of_the_prior_ones_cached_order()
    {
        var victim = MakeVictimWithHand(1, 5);
        var players = new List<Player> { new(0), victim };

        var steal = new StealAttempt();
        steal.Begin(thiefIndex: 0, victimIndex: 1, StealTargetZone.Hand);
        var firstAttemptView = steal.BuildPhaseView(GameState.AwaitingStealCardPick, 0, players, new Random(11));
        var firstOrder = firstAttemptView!.ThiefPickSlots!.Select(s => s.CardId).ToArray();

        // Attempt ends (steal resolved or aborted) and a new one begins over the same victim.
        steal.Clear();
        Assert.Null(steal.ThiefPickOrder);

        steal.Begin(thiefIndex: 0, victimIndex: 1, StealTargetZone.Hand);
        var secondAttemptView = steal.BuildPhaseView(GameState.AwaitingStealCardPick, 0, players, new Random(22));
        var secondOrder = secondAttemptView!.ThiefPickSlots!.Select(s => s.CardId).ToArray();

        Assert.NotEqual(firstOrder, secondOrder);
    }

    [Fact]
    public void GameSession_two_back_to_back_views_for_the_thief_return_the_same_pick_slot_order()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var deck = new Deck();
        var session = new GameSession(new[] { p0, p1 }, deck);
        session.ChooseShinyStealVictim = (_, candidates) => candidates[0];

        p1.AddToStash(new Card(CardName.MmmPie), faceUp: false);
        p1.AddToStash(new Card(CardName.Feesh), faceUp: false);
        p1.AddToStash(new Card(CardName.Nanners), faceUp: false);
        p1.AddToStash(new Card(CardName.Doggo), faceUp: false);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));

        var die = new Die();
        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out var err1, out _), err1);
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var err2, out _), err2);
        Assert.Equal(GameState.AwaitingStealCardPick, session.State);

        var firstView = session.GetViewForPlayer(0);
        var secondView = session.GetViewForPlayer(0);

        Assert.NotNull(firstView.StealPhase?.ThiefPickSlots);
        Assert.NotNull(secondView.StealPhase?.ThiefPickSlots);
        Assert.Equal(
            firstView.StealPhase!.ThiefPickSlots!.Select(s => s.CardId),
            secondView.StealPhase!.ThiefPickSlots!.Select(s => s.CardId));
    }
}
