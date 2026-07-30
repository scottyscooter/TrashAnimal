using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Regression tests for the Steal-token exhaustion bug (resolving Steal via the API's explicit-choice
/// path never touched <c>TokenPhaseState.RemainingTokens</c>/<c>ActiveToken</c>, so it never exhausted)
/// and the related "stuck forever" bugs where a token with no valid effect (Steal with zero hand
/// candidates, Bandit with an empty deck) had no auto-resolve path. See
/// <c>.claude/docs/plans/steal-token-resolution-fix.md</c>.
/// </summary>
public sealed class TokenPhaseStealExhaustionTests
{
    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInTokenPhase(
        Die die, IDrawPile drawPile)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, drawPile);

        session.ApplyAction(0, GameAction.RollDie, die, out var rollError, out _);
        Assert.True(session.PhaseOne.Tokens.Count > 0, rollError);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        return (p0, p1, session);
    }

    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInTokenPhaseWithTwoTokens(
        Die die, IDrawPile drawPile)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, drawPile);

        session.ApplyAction(0, GameAction.RollDie, die, out _, out _);
        session.ApplyAction(0, GameAction.RollDie, die, out _, out _);
        Assert.Equal(2, session.PhaseOne.Tokens.Count);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        return (p0, p1, session);
    }

    [Fact]
    public void ResolveTokenSteal_via_explicit_victim_choice_removes_token_and_ends_turn_when_last_token()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(5).Object;
        var (_, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh));

        var succeeded = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 1, out var error, out var resolvedWithNoEffectToken);
        Assert.True(succeeded, error);
        Assert.Null(resolvedWithNoEffectToken);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);

        // Complete the steal like any other steal attempt (victim passes, thief picks a card).
        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var passError, out _), passError);
        Assert.Equal(GameState.AwaitingStealCardPick, session.State);
        var stolenCardId = p1.Hand[0].Card.Id;
        Assert.True(session.TryCompleteStealWithCard(0, stolenCardId, out var pickError, out _), pickError);

        // Regression assertion for bug #1: Steal was the only token, so it must be removed from
        // RemainingTokens and the turn must end — previously RemainingTokens was never touched by this
        // API path, so the turn could never complete.
        Assert.Equal(GameState.TurnEnd, session.State);
        Assert.DoesNotContain(GameAction.ResolveTokenSteal, session.GetAllowedActionsForPlayer(0));
    }

    [Fact]
    public void ResolveTokenSteal_with_zero_candidates_auto_resolves_and_ends_turn_when_last_token()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(5).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        Assert.Empty(p1.Hand); // no opponent has any card in hand — the fizzle condition

        var succeeded = session.TryStartTokenStealWithVictimChoice(0, victimIndex: null, out var error, out var resolvedWithNoEffectToken);

        Assert.True(succeeded, error);
        Assert.Equal(TokenAction.Steal, resolvedWithNoEffectToken);
        Assert.Equal(GameState.TurnEnd, session.State);

        var log = session.GetViewForPlayer(0).Log;
        Assert.Contains(log, e => e.Message.Contains("picked a Steal token, but no opponent had a card"));
    }

    [Fact]
    public void ResolveTokenSteal_with_zero_candidates_removes_only_steal_when_other_tokens_remain()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal, TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(5).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhaseWithTwoTokens(die, drawPile);
        Assert.Empty(p1.Hand);

        var succeeded = session.TryStartTokenStealWithVictimChoice(0, victimIndex: null, out var error, out var resolvedWithNoEffectToken);

        Assert.True(succeeded, error);
        Assert.Equal(TokenAction.Steal, resolvedWithNoEffectToken);
        Assert.Equal(GameState.TokenPhase, session.State);

        var remaining = session.GetViewForPlayer(0).TokenPhase!.RemainingTokens;
        Assert.DoesNotContain(TokenAction.Steal, remaining);
        Assert.Contains(TokenAction.StashTrash, remaining);
    }

    [Fact]
    public void ResolveTokenSteal_rejects_explicit_victim_when_no_candidates_exist()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(5).Object;
        var (_, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        Assert.Empty(p1.Hand);

        var succeeded = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 1, out var error, out var resolvedWithNoEffectToken);

        Assert.False(succeeded);
        Assert.NotNull(error);
        Assert.Null(resolvedWithNoEffectToken);
        Assert.Equal(GameState.TokenPhase, session.State);
    }

    [Fact]
    public void ResolveTokenBandit_with_empty_deck_auto_resolves_and_ends_turn_when_last_token()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit).Object;
        var drawPile = DrawPileMockFactory.CreateEmpty().Object;
        var (p0, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile);

        // Regression coverage for the general fizzle-signal fix: a first-pick Bandit-with-empty-deck fizzle
        // triggered via the plain GameAction.ResolveTokenBandit path (not the dedicated Steal API) must
        // surface the fizzled token straight from ApplyAction's out param — this was silently broken even
        // without MmmPie, since only ExecuteTokenStealUnlockedAsync's bespoke bool ever reported a fizzle.
        var succeeded = session.ApplyAction(0, GameAction.ResolveTokenBandit, die, out var error, out var resolvedWithNoEffectToken);

        Assert.True(succeeded, error);
        Assert.Equal(TokenAction.Bandit, resolvedWithNoEffectToken);
        Assert.Equal(GameState.TurnEnd, session.State);

        var log = session.GetViewForPlayer(0).Log;
        Assert.Contains(log, e => e.Message.Contains("picked a Bandit token, but the deck was empty"));
    }

    [Fact]
    public void MmmPie_then_completing_first_steal_against_lone_card_cascades_into_fizzled_repeat()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(5).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile);
        p1.Hand.Add(new Card(CardName.Feesh)); // exactly one card — the first steal succeeds, then p1's hand is empty
        p0.Hand.Add(new Card(CardName.MmmPie));

        // Play MmmPie before picking Steal — it flags ResolveTokenTwice for whichever token gets resolved next.
        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        // Start Steal via the API's explicit-victim-choice path — succeeds normally against the 1-card opponent.
        var started = session.TryStartTokenStealWithVictimChoice(0, victimIndex: 1, out var startError, out var startFizzle);
        Assert.True(started, startError);
        Assert.Null(startFizzle);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);

        Assert.True(session.ApplyAction(1, GameAction.StealPass, die, out var passError, out _), passError);
        Assert.Equal(GameState.AwaitingStealCardPick, session.State);
        var stolenCardId = p1.Hand[0].Card.Id;

        // Completing this pick finishes the first Steal resolution, which triggers MmmPie's repeat cascade:
        // Steal restarts immediately via TokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase, finds zero
        // remaining candidates (the victim's hand is now empty), and fizzles. The signal must surface here,
        // from TryCompleteStealWithCard's own out param — not just as a game-log entry — since this fizzle
        // happens as a side effect of a completely different command (CardPickCommand) than the one that
        // started the token.
        var completed = session.TryCompleteStealWithCard(0, stolenCardId, out var pickError, out var cascadeFizzle);

        Assert.True(completed, pickError);
        Assert.Equal(TokenAction.Steal, cascadeFizzle);
        Assert.Equal(GameState.TurnEnd, session.State);

        var log = session.GetViewForPlayer(0).Log;
        Assert.Contains(log, e => e.Message.Contains("picked a Steal token, but no opponent had a card"));
    }

    [Fact]
    public void MmmPie_then_fizzled_steal_does_not_leak_ResolveTokenTwice_onto_next_token()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal, TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(5).Object;
        var (p0, p1, session) = CreateApiModeSessionInTokenPhaseWithTwoTokens(die, drawPile);
        Assert.Empty(p1.Hand); // ensures Steal fizzles with zero candidates
        p0.Hand.Add(new Card(CardName.MmmPie));

        // Play MmmPie before picking any token — it flags ResolveTokenTwice for whichever token gets
        // picked/finished next.
        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        // Pick Steal — it fizzles immediately (zero candidates) via GameAction.ResolveTokenSteal, which
        // routes through TryStartToken's Steal branch without needing ChooseTokenHandStealVictim configured.
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenSteal, die, out var stealError, out _), stealError);
        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.DoesNotContain(TokenAction.Steal, session.GetViewForPlayer(0).TokenPhase!.RemainingTokens);

        // Pick the second, normal token (StashTrash) and resolve it once.
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenStashTrash, die, out var stashTrashError, out _), stashTrashError);
        Assert.True(session.ApplyAction(0, GameAction.TokenStashTrashDrawOne, die, out var drawError, out _), drawError);

        // If ResolveTokenTwice had leaked from the fizzled Steal, StashTrash would restart instead of
        // finishing — the turn would still be in TokenPhase at the StashTrash sub-step instead of ending.
        Assert.Equal(GameState.TurnEnd, session.State);
    }
}
