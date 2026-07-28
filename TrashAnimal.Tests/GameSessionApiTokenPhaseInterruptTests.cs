using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// TokenPhase counterpart to <see cref="GameSessionApiCardPlayTests"/>. Shiny and Feesh can also be
/// played as interrupts during TokenPhase, and — like their RollPhase equivalents — the API drives
/// those plays through explicit-choice methods rather than the CLI's <c>Func&lt;&gt;</c> selectors.
///
/// These tests pin both halves of that contract: the actions surface in
/// <c>GetAllowedActionsForPlayer</c> with no delegate wired, and the explicit-choice methods apply
/// the play correctly. Previously both were gated on the delegates being non-null, which made the
/// plays unreachable over HTTP entirely.
/// </summary>
public sealed class GameSessionApiTokenPhaseInterruptTests
{
    /// <summary>
    /// Creates a two-player session driven to TokenPhase with NO delegate selectors — the API mode
    /// baseline. Both hands are cleared so tests seed exactly the cards they need.
    /// </summary>
    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInTokenPhase()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;

        session.ApplyAction(0, GameAction.RollDie, die, out _);
        session.ApplyAction(0, GameAction.StopRolling, die, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        p0.Hand.Clear();
        p1.Hand.Clear();
        return (p0, p1, session);
    }

    [Fact]
    public void PlayShinyTokenPhase_appears_in_allowed_actions_without_delegate()
    {
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Shiny));
        p1.AddToStash(new Card(CardName.MmmPie), faceUp: true);

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.Contains(GameAction.PlayShinyTokenPhase, allowed);
    }

    [Fact]
    public void PlayFeeshTokenPhase_appears_in_allowed_actions_without_delegate()
    {
        var (p0, _, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Feesh));
        session.DiscardPile.Add(new Card(CardName.MmmPie));

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.Contains(GameAction.PlayFeeshTokenPhase, allowed);
    }

    [Fact]
    public void PlayShinyTokenPhase_not_offered_when_no_opponent_has_stash()
    {
        var (p0, _, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Shiny));

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.DoesNotContain(GameAction.PlayShinyTokenPhase, allowed);
    }

    [Fact]
    public void TryPlayShinyTokenPhaseWithVictimChoice_begins_steal_without_delegate()
    {
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Shiny));
        p1.AddToStash(new Card(CardName.MmmPie), faceUp: true);

        var succeeded = session.TryPlayShinyTokenPhaseWithVictimChoice(0, victimIndex: 1, out var error);

        Assert.True(succeeded, error);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);
        Assert.Equal(0, session.StealThiefIndex);
        Assert.Equal(1, session.StealVictimIndex);
        Assert.Contains(session.DiscardPile, c => c.Name == CardName.Shiny);
    }

    [Fact]
    public void TryPlayFeeshTokenPhaseWithCardChoice_retrieves_selected_card_without_delegate()
    {
        var (p0, _, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Feesh));
        var target = new Card(CardName.MmmPie);
        session.DiscardPile.Add(target);

        var succeeded = session.TryPlayFeeshTokenPhaseWithCardChoice(0, target.Id, out var error);

        Assert.True(succeeded, error);
        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Contains(target, p0.Hand.Select(e => e.Card));
        Assert.DoesNotContain(session.DiscardPile, c => c.Id == target.Id);
        Assert.Contains(session.DiscardPile, c => c.Name == CardName.Feesh);
    }

    [Fact]
    public void TryPlayShinyTokenPhaseWithVictimChoice_rejects_victim_with_empty_stash()
    {
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Shiny));
        p1.AddToStash(new Card(CardName.MmmPie), faceUp: true);

        var succeeded = session.TryPlayShinyTokenPhaseWithVictimChoice(0, victimIndex: 0, out var error);

        Assert.False(succeeded);
        Assert.NotNull(error);
        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Contains(p0.Hand, e => e.Card.Name == CardName.Shiny);
    }

    [Fact]
    public void TryPlayFeeshTokenPhaseWithCardChoice_rejects_card_not_in_discard()
    {
        var (p0, _, session) = CreateApiModeSessionInTokenPhase();
        p0.Hand.Add(new Card(CardName.Feesh));
        session.DiscardPile.Add(new Card(CardName.MmmPie));

        var succeeded = session.TryPlayFeeshTokenPhaseWithCardChoice(0, Guid.NewGuid(), out var error);

        Assert.False(succeeded);
        Assert.NotNull(error);
        Assert.Contains(p0.Hand, e => e.Card.Name == CardName.Feesh);
    }

    [Fact]
    public void TokenPhase_explicit_choice_methods_reject_when_not_in_token_phase()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, new Deck());
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        p0.Hand.Add(new Card(CardName.Feesh));
        p1.AddToStash(new Card(CardName.MmmPie), faceUp: true);
        var target = new Card(CardName.Kitteh);
        session.DiscardPile.Add(target);

        Assert.Equal(GameState.RollPhase, session.State);
        Assert.False(session.TryPlayShinyTokenPhaseWithVictimChoice(0, 1, out var shinyError));
        Assert.NotNull(shinyError);
        Assert.False(session.TryPlayFeeshTokenPhaseWithCardChoice(0, target.Id, out var feeshError));
        Assert.NotNull(feeshError);
    }
}
