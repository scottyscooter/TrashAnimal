using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

public sealed class GameSessionShinyStealTests
{
    private static (Player p0, Player p1, Deck deck, GameSession session) CreateTwoPlayerSession()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var deck = new Deck();
        var session = new GameSession(new[] { p0, p1 }, new PhaseTwoNoop(), deck);
        session.ChooseShinyStealVictim = (_, candidates) => candidates[0];
        session.OnFeeshCardSelection = (_, __) => null;
        return (p0, p1, deck, session);
    }

    [Fact]
    public void PlayShiny_not_allowed_when_all_opponent_stashes_empty()
    {
        var (p0, _, deck, session) = CreateTwoPlayerSession();
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));

        var allowed = session.GetAllowedActionsForPlayer(0);
        Assert.DoesNotContain(GameAction.PlayShiny, allowed);
    }

    [Fact]
    public void PlayShiny_allowed_when_opponent_has_stash()
    {
        var (p0, p1, _, session) = CreateTwoPlayerSession();
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        p1.AddToStash(new Card(CardName.Nanners), faceUp: true);

        var allowed = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayShiny, allowed);
    }

    [Fact]
    public void Steal_pass_then_thief_takes_card_from_stash()
    {
        var (p0, p1, _, session) = CreateTwoPlayerSession();
        var stashed = new Card(CardName.MmmPie);
        p1.AddToStash(stashed, faceUp: false);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));

        var die = new Die();
        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out var err1), err1);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);

        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var err2), err2);
        Assert.Equal(GameState.AwaitingStealCardPick, session.State);

        Assert.True(session.TryCompleteStealWithCard(0, stashed.Id, out var err3), err3);
        Assert.Equal(GameState.RollPhase, session.State);
        Assert.Contains(stashed, p0.Hand);
        Assert.DoesNotContain(p1.StashPile, e => e.Card.Id == stashed.Id);
    }

    [Fact]
    public void Steal_doggo_blocks_and_victim_draws_up_to_two()
    {
        var (p0, p1, deck, session) = CreateTwoPlayerSession();
        p1.AddToStash(new Card(CardName.Blammo), faceUp: true);
        p1.Hand.Add(new Card(CardName.Doggo));
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));

        var before = deck.GetDeckCount();
        var die = new Die();
        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _));
        Assert.True(session.ApplyAction(1, GameAction.StealPlayDoggo, die, out var err), err);
        Assert.Equal(GameState.RollPhase, session.State);
        Assert.Equal(before - 2, deck.GetDeckCount());
        Assert.Equal(2, p1.Hand.Count(c => c.Name is not CardName.Doggo));
    }

    [Fact]
    public void Steal_kitteh_swaps_roles_initial_zone_stays_stash()
    {
        var (p0, p1, _, session) = CreateTwoPlayerSession();
        var victimCard = new Card(CardName.Feesh);
        p0.AddToStash(victimCard, faceUp: true);
        p1.AddToStash(new Card(CardName.Nanners), faceUp: true);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        p1.Hand.Add(new Card(CardName.Kitteh));

        session.ChooseShinyStealVictim = (_, _) => 1;

        var die = new Die();
        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _));
        Assert.True(session.ApplyAction(1, GameAction.StealPlayKitteh, die, out _));

        Assert.Equal(1, session.StealThiefIndex);
        Assert.Equal(0, session.StealVictimIndex);
        Assert.Equal(StealTargetZone.Stash, session.InitialStealTargetZone);

        var view = session.GetViewForPlayer(0);
        Assert.NotNull(view.StealPhase);
        Assert.Equal(StealTargetZone.Stash, view.StealPhase!.InitialStealTargetZone);

        Assert.True(session.ApplyAction(0, GameAction.StealPass, die, out _));
        Assert.True(session.TryCompleteStealWithCard(1, p0.StashPile[0].Card.Id, out _), "Bob steals Alice's stash card");
        Assert.Contains(victimCard, p1.Hand);
    }
}
