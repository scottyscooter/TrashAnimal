using TrashAnimal;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Covers <c>scenarios.txt</c> lines 1–32: voluntary stop, empty/non-empty discard, Feesh/Shiny availability, and stash steal chains.
/// </summary>
public sealed class GameSessionVoluntaryStopScenariosTests
{
    private sealed class SequencedDie : Die
    {
        private readonly Queue<TokenAction> _sequence;

        public SequencedDie(params TokenAction[] sequence) : base(Random.Shared) =>
            _sequence = new Queue<TokenAction>(sequence);

        public override TokenAction Roll() =>
            _sequence.Count > 0 ? _sequence.Dequeue() : TokenAction.StashTrash;
    }

    private static (Player p0, Player p1, GameSession session) CreateTwoPlayerSession(
        Func<int, IReadOnlyList<Card>, Card?>? onFeeshCardSelection,
        Func<int, IReadOnlyList<int>, int>? chooseShinyVictim)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var deck = new Deck();
        var session = new GameSession(new[] { p0, p1 }, deck);
        session.OnFeeshCardSelection = onFeeshCardSelection;
        session.ChooseShinyStealVictim = chooseShinyVictim;
        return (p0, p1, session);
    }

    private static void PassEntireYumYumWindow(GameSession session, Die die)
    {
        while (session.State == GameState.AwaitingYumYum)
        {
            var responder = session.GetCurrentYumYumResponderIndex();
            Assert.NotNull(responder);
            Assert.True(session.ApplyAction(responder.Value, GameAction.YumYumPass, die, out var err), err);
        }
    }

    private static void RollOnceThenVoluntaryStop(GameSession session, Die die)
    {
        Assert.True(session.ApplyAction(0, GameAction.RollDie, die, out var err1), err1);
        Assert.True(session.ApplyAction(0, GameAction.StopRolling, die, out var err2), err2);
        Assert.Equal(GameState.AwaitingYumYum, session.State);
    }

    private static Card FirstCardNamed(IReadOnlyList<Card> discard, CardName name) =>
        discard.First(c => c.Name == name);

    [Fact]
    public void VoluntaryStop_empty_discard_feesh_not_shown_then_advance_to_token_phase()
    {
        var (p0, p1, session) = CreateTwoPlayerSession(
            onFeeshCardSelection: (_, _) => throw new InvalidOperationException("Feesh selection must not run when discard is empty."),
            chooseShinyVictim: (_, _) => 1);
        p1.StashPile.Clear();
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Blammo));
        p0.Hand.Add(new Card(CardName.MmmPie));
        p0.Hand.Add(new Card(CardName.Feesh));
        p0.Hand.Add(new Card(CardName.Blammo));
        p0.Hand.Add(new Card(CardName.Shiny));

        var die = new SequencedDie(TokenAction.Bandit);
        RollOnceThenVoluntaryStop(session, die);
        PassEntireYumYumWindow(session, die);
        Assert.Equal(GameState.RollPhase, session.State);

        var afterWindow = session.GetAllowedActionsForPlayer(0);
        Assert.DoesNotContain(GameAction.PlayFeesh, afterWindow);
        Assert.DoesNotContain(GameAction.PlayShiny, afterWindow);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterWindow);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out var err), err);
        Assert.Equal(GameState.TokenPhase, session.State);
    }

    [Fact]
    public void VoluntaryStop_discard_has_nanners_play_feesh_then_advance_hand_keeps_shiny_and_gains_nanners()
    {
        var nannersOnDiscard = new Card(CardName.Nanners);
        var shinyInHand = new Card(CardName.Shiny);
        var (p0, p1, session) = CreateTwoPlayerSession(
            (_, discard) => FirstCardNamed(discard, CardName.Nanners),
            chooseShinyVictim: (_, _) => 1);
        p1.StashPile.Clear();
        session.DiscardPile.Add(nannersOnDiscard);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Feesh));
        p0.Hand.Add(shinyInHand);

        var die = new SequencedDie(TokenAction.Recycle);
        RollOnceThenVoluntaryStop(session, die);
        PassEntireYumYumWindow(session, die);

        var mid = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayFeesh, mid);
        Assert.Contains(GameAction.AdvanceToResolveTokens, mid);
        Assert.DoesNotContain(GameAction.PlayShiny, mid);

        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out var err1), err1);
        Assert.Contains(shinyInHand, p0.Hand.Select(e => e.Card));
        Assert.Contains(nannersOnDiscard, p0.Hand.Select(e => e.Card));

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out var err2), err2);
        Assert.Equal(GameState.TokenPhase, session.State);
    }

    [Fact]
    public void VoluntaryStop_discard_shiny_opponent_stash_nanners_hand_only_feesh_then_shiny_and_advance_options()
    {
        var shinyOnDiscard = new Card(CardName.Shiny);
        var stashedNanners = new Card(CardName.Nanners);
        var (p0, p1, session) = CreateTwoPlayerSession(
            (_, discard) => FirstCardNamed(discard, CardName.Shiny),
            chooseShinyVictim: (_, _) => 1);
        p1.StashPile.Clear();
        p1.AddToStash(stashedNanners, faceUp: true);
        session.DiscardPile.Add(shinyOnDiscard);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Feesh));

        var die = new SequencedDie(TokenAction.DoubleTrash);
        RollOnceThenVoluntaryStop(session, die);
        PassEntireYumYumWindow(session, die);

        Assert.True(session.ApplyAction(0, GameAction.PlayFeesh, die, out var err1), err1);
        Assert.Contains(shinyOnDiscard, p0.Hand.Select(e => e.Card));
        Assert.DoesNotContain(p0.Hand, e => e.Card.Name == CardName.Feesh);

        var afterFeesh = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayShiny, afterFeesh);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterFeesh);
    }

    [Fact]
    public void VoluntaryStop_all_stashes_empty_discard_empty_only_advance_no_feesh()
    {
        var (p0, p1, session) = CreateTwoPlayerSession(
            onFeeshCardSelection: (_, _) => throw new InvalidOperationException(),
            chooseShinyVictim: (_, _) => 1);
        p1.StashPile.Clear();
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Blammo));
        p0.Hand.Add(new Card(CardName.MmmPie));
        p0.Hand.Add(new Card(CardName.Feesh));
        p0.Hand.Add(new Card(CardName.Blammo));
        p0.Hand.Add(new Card(CardName.Shiny));

        var die = new SequencedDie(TokenAction.Steal);
        RollOnceThenVoluntaryStop(session, die);
        PassEntireYumYumWindow(session, die);

        var actions = session.GetAllowedActionsForPlayer(0);
        Assert.DoesNotContain(GameAction.PlayFeesh, actions);
        Assert.DoesNotContain(GameAction.PlayShiny, actions);
        Assert.Contains(GameAction.AdvanceToResolveTokens, actions);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out var err), err);
        Assert.Equal(GameState.TokenPhase, session.State);
    }

    [Fact]
    public void VoluntaryStop_discard_empty_hand_shiny_steal_from_stash_then_advance()
    {
        var stashedPie = new Card(CardName.MmmPie);
        var shinyInHand = new Card(CardName.Shiny);
        var (p0, p1, session) = CreateTwoPlayerSession(
            onFeeshCardSelection: null,
            chooseShinyVictim: (_, _) => 1);
        p1.StashPile.Clear();
        p1.AddToStash(stashedPie, faceUp: false);
        p0.Hand.Clear();
        p0.Hand.Add(shinyInHand);

        var die = new SequencedDie(TokenAction.StashTrash);
        RollOnceThenVoluntaryStop(session, die);
        PassEntireYumYumWindow(session, die);

        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out var err1), err1);
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var err2), err2);
        Assert.True(session.TryCompleteStealWithCard(0, stashedPie.Id, out var err3), err3);
        Assert.Contains(stashedPie, p0.Hand.Select(e => e.Card));
        Assert.Empty(p1.StashPile);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out var err4), err4);
        Assert.Equal(GameState.TokenPhase, session.State);
    }

    [Fact]
    public void VoluntaryStop_feesh_shiny_and_nanners_on_discard_play_shiny_first_hand_ends_feesh_and_mmm_pie()
    {
        var mmmPieInStash = new Card(CardName.MmmPie);
        var nannersOnDiscard = new Card(CardName.Nanners);
        var feeshInHand = new Card(CardName.Feesh);
        var shinyInHand = new Card(CardName.Shiny);
        var (p0, p1, session) = CreateTwoPlayerSession(
            (_, discard) => FirstCardNamed(discard, CardName.Nanners),
            chooseShinyVictim: (_, _) => 1);
        p1.StashPile.Clear();
        p1.AddToStash(mmmPieInStash, faceUp: false);
        session.DiscardPile.Add(nannersOnDiscard);
        p0.Hand.Clear();
        p0.Hand.Add(feeshInHand);
        p0.Hand.Add(shinyInHand);

        var die = new SequencedDie(TokenAction.DoubleStash);
        RollOnceThenVoluntaryStop(session, die);
        PassEntireYumYumWindow(session, die);

        var afterWindow = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayFeesh, afterWindow);
        Assert.Contains(GameAction.PlayShiny, afterWindow);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterWindow);

        Assert.True(session.ApplyAction(0, GameAction.PlayShiny, die, out var err1), err1);
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var err2), err2);
        Assert.True(session.TryCompleteStealWithCard(0, mmmPieInStash.Id, out var err3), err3);

        Assert.Contains(feeshInHand, p0.Hand.Select(e => e.Card));
        Assert.Contains(mmmPieInStash, p0.Hand.Select(e => e.Card));

        var afterSteal = session.GetAllowedActionsForPlayer(0);
        Assert.Contains(GameAction.PlayFeesh, afterSteal);
        Assert.Contains(GameAction.AdvanceToResolveTokens, afterSteal);

        Assert.True(session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out var err4), err4);
        Assert.Equal(GameState.TokenPhase, session.State);
    }
}
