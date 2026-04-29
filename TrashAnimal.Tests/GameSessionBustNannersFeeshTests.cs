using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Covers <c>scenarios.txt</c> "Busting with Nanners and Feesh" (empty discard, MmmPie, Shiny with/without stash steal chains).
/// </summary>
public sealed class GameSessionBustNannersFeeshTests
{
    private sealed class SequencedDie : Die
    {
        private readonly Queue<TokenAction> _sequence;

        public SequencedDie(params TokenAction[] sequence) : base(Random.Shared) =>
            _sequence = new Queue<TokenAction>(sequence);

        public override TokenAction Roll() =>
            _sequence.Count > 0 ? _sequence.Dequeue() : TokenAction.StashTrash;
    }

    private static (Player p0, Player p1, Deck deck, GameSession session) CreateSession(
        Func<int, IReadOnlyList<Card>, Card?>? onFeeshCardSelection)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var deck = new Deck();
        var session = new GameSession(new[] { p0, p1 }, new PhaseTwoNoop(), deck);
        session.ChooseShinyStealVictim = (_, candidates) => candidates[0];
        session.OnFeeshCardSelection = onFeeshCardSelection;
        return (p0, p1, deck, session);
    }

    private static void BustWithTwoIdenticalRolls(GameSession session, Die die)
    {
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.False(session.PhaseOne.IsBusted);
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out _));
        Assert.True(session.PhaseOne.IsBusted);
    }

    private static Card FirstCardNamed(IReadOnlyList<Card> discard, CardName name) =>
        discard.First(c => c.Name == name);

    [Fact]
    public void Bust_empty_discard_nanners_then_only_advance_reaches_token_phase()
    {
        var (p0, _, _, session) = CreateSession(onFeeshCardSelection: null);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        BustWithTwoIdenticalRolls(session, die);

        var bustActions = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayNanners, bustActions);
        Assert.Contains(GameAction.AbandonBust, bustActions);
        Assert.DoesNotContain(GameAction.PlayFeesh, bustActions);

        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out var err1), err1);
        Assert.False(session.PhaseOne.IsBusted);

        var afterNanners = session.GetAllowedActionsForPlayer(0);
        Assert.DoesNotContain(GameAction.PlayFeesh, afterNanners);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterNanners);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out var err2), err2);
        Assert.Equal(GameState.TurnEnd, session.State);
        Assert.Single(session.LastPhaseTwoTokens);
        Assert.Equal(TokenAction.Bandit, session.LastPhaseTwoTokens[0]);
    }

    [Fact]
    public void Bust_discard_has_mmm_pie_nanners_then_feesh_retrieves_pie_then_advance()
    {
        var mmmPie = new Card(CardName.MmmPie);
        var (p0, _, _, session) = CreateSession((_, discard) => FirstCardNamed(discard, CardName.MmmPie));
        session.DiscardPile.Add(mmmPie);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.Recycle, TokenAction.Recycle);
        BustWithTwoIdenticalRolls(session, die);

        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out _));

        var afterNanners = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayFeesh, afterNanners);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterNanners);

        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out var err), err);
        Assert.Contains(mmmPie, p0.Hand.Select(e => e.Card));
        Assert.DoesNotContain(session.DiscardPile, c => c.Id == mmmPie.Id);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _));
        Assert.Equal(GameState.TurnEnd, session.State);
    }

    [Fact]
    public void Bust_discard_has_shiny_no_opponent_stash_feesh_takes_shiny_then_advance()
    {
        var shiny = new Card(CardName.Shiny);
        var (p0, p1, _, session) = CreateSession((_, discard) => FirstCardNamed(discard, CardName.Shiny));
        session.DiscardPile.Add(shiny);
        p1.StashPile.Clear();
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.Steal, TokenAction.Steal);
        BustWithTwoIdenticalRolls(session, die);

        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out _));
        Assert.Contains(shiny, p0.Hand.Select(e => e.Card));
        Assert.DoesNotContain(GameAction.PlayShiny, session.GetAllowedActionsForPlayer(0));

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _));
        Assert.Equal(GameState.TurnEnd, session.State);
    }

    [Fact]
    public void Bust_discard_shiny_stash_feesh_steal_then_advance_leaves_expected_discard_and_empty_stash()
    {
        var shinyOnDiscard = new Card(CardName.Shiny);
        var stashedFeesh = new Card(CardName.Feesh);
        var (p0, p1, _, session) = CreateSession((_, discard) => FirstCardNamed(discard, CardName.Shiny));
        session.DiscardPile.Add(shinyOnDiscard);
        p1.StashPile.Clear();
        p1.AddToStash(stashedFeesh, faceUp: true);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.DoubleTrash, TokenAction.DoubleTrash);
        BustWithTwoIdenticalRolls(session, die);

        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out _));
        Assert.Contains(shinyOnDiscard, p0.Hand.Select(e => e.Card));

        var afterFeesh = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayShiny, afterFeesh);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterFeesh);

        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _));
        Assert.Equal(GameState.AwaitingStealResponse, session.State);
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out _));
        Assert.True(session.TryCompleteStealWithCard(0, stashedFeesh.Id, out var pickErr), pickErr);

        Assert.Contains(stashedFeesh, p0.Hand.Select(e => e.Card));
        Assert.Empty(p1.StashPile);

        var beforeAdvance = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayFeesh, beforeAdvance);
        Assert.Contains(GameAction.AdvanceToResolveTokens, beforeAdvance);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _));
        Assert.Equal(GameState.TurnEnd, session.State);

        var names = session.DiscardPile.Select(c => c.Name).ToArray();
        Assert.Contains(CardName.Shiny, names);
        Assert.Equal(1, names.Count(n => n == CardName.Feesh));
        Assert.Contains(CardName.Nanners, names);
    }

    [Fact]
    public void Bust_discard_shiny_stash_feesh_second_feesh_takes_shiny_back_discard_ends_two_feesh()
    {
        var shinyOnDiscard = new Card(CardName.Shiny);
        var stashedFeesh = new Card(CardName.Feesh);
        var (p0, p1, _, session) = CreateSession((_, discard) => FirstCardNamed(discard, CardName.Shiny));
        session.DiscardPile.Add(shinyOnDiscard);
        p1.StashPile.Clear();
        p1.AddToStash(stashedFeesh, faceUp: true);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Nanners));
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.StashTrash, TokenAction.StashTrash);
        BustWithTwoIdenticalRolls(session, die);

        Assert.True(session.ApplyAction(0, GameAction.PlayNanners, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out _));
        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out _));
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out _));
        Assert.True(session.TryCompleteStealWithCard(0, stashedFeesh.Id, out _));

        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out var feesh2Err), feesh2Err);
        Assert.Contains(shinyOnDiscard, p0.Hand.Select(e => e.Card));
        Assert.Empty(p1.StashPile);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _));
        Assert.Equal(GameState.TurnEnd, session.State);

        Assert.Equal(2, session.DiscardPile.Count(c => c.Name == CardName.Feesh));
        Assert.DoesNotContain(session.DiscardPile, c => c.Name == CardName.Shiny);
    }
}
