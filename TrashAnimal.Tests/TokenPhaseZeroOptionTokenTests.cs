using TrashAnimal.TokenPhase;
using TrashAnimal.Tests.TestSupport;
using Xunit;

namespace TrashAnimal.Tests;

/// <summary>
/// Regression tests for two more "reachable with zero legal moves" TokenPhase strands, found by
/// auditing all six <see cref="TokenAction"/> types rather than only the ones named in the original
/// bug report (which is how Steal/Bandit's equivalent bugs were fixed earlier on this branch):
/// Recycle with zero replacement options (fizzles, like Steal/Bandit), and StashTrash's stash-mode
/// branch with zero stashable hand cards (gated instead, since the draw branch is always available).
/// Also covers the related Recycle-repeat bug where a Recycle pick could select a token that had
/// already been placed into <see cref="TokenPhaseState.RemainingTokens"/> earlier in the same
/// TokenPhase. See <c>.claude/docs/plans/token-zero-option-deadlocks-fix.md</c>.
/// </summary>
public sealed class TokenPhaseZeroOptionTokenTests
{
    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInTokenPhase(
        Die die, IDrawPile drawPile, int rollCount)
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, drawPile);

        for (var i = 0; i < rollCount; i++)
            session.ApplyAction(0, GameAction.RollDie, die, out var rollError, out _);

        Assert.Equal(rollCount, session.PhaseOne.Tokens.Count);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        return (p0, p1, session);
    }

    // --- B1: Recycle with zero replacement options -------------------------------------------------

    [Fact]
    public void Recycle_WithAllSixTokensRolled_AutoResolvesWithNoEffect()
    {
        var die = DieMockFactory.CreateSequenced(
            TokenAction.StashTrash, TokenAction.DoubleStash, TokenAction.DoubleTrash,
            TokenAction.Bandit, TokenAction.Steal, TokenAction.Recycle).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 6);

        var succeeded = session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var error, out var resolvedWithNoEffectToken);

        Assert.True(succeeded, error);
        Assert.Equal(TokenAction.Recycle, resolvedWithNoEffectToken);

        var tokenPhase = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Null(tokenPhase.ActiveToken);
        Assert.DoesNotContain(TokenAction.Recycle, tokenPhase.RemainingTokens);
        Assert.Equal(TokenPhaseStep.ChoosingNextToken, tokenPhase.Step);
        Assert.NotEqual(GameState.TurnEnd, session.State);

        var log = session.GetViewForPlayer(0).Log;
        Assert.Contains(log, e => e.Message.Contains("picked a Recycle token, but there was no unrolled token to swap in"));
    }

    [Fact]
    public void Recycle_WithAtLeastOneUnrolledToken_StillEntersReplacementStep()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Recycle).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 1);

        var succeeded = session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var error, out var resolvedWithNoEffectToken);

        Assert.True(succeeded, error);
        Assert.Null(resolvedWithNoEffectToken);

        var tokenPhase = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Equal(TokenPhaseStep.RecycleChoosingReplacement, tokenPhase.Step);
        Assert.NotEmpty(tokenPhase.RecycleReplacementOptions);
    }

    [Fact]
    public void MmmPieRepeatOfRecycle_WithNoOptions_AutoResolvesWithNoEffect()
    {
        // Rolling every face except Steal leaves exactly one Recycle option (Steal itself) on the
        // first pick; picking it exhausts every token type, so the MmmPie repeat (RestartSubflow) has
        // zero options left and must fizzle instead of stranding the turn.
        var die = DieMockFactory.CreateSequenced(
            TokenAction.Recycle, TokenAction.StashTrash, TokenAction.DoubleStash,
            TokenAction.DoubleTrash, TokenAction.Bandit).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 5);
        p0.Hand.Add(new Card(CardName.MmmPie));

        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);

        var started = session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var startError, out var startFizzle);
        Assert.True(started, startError);
        Assert.Null(startFizzle);
        var tokenPhaseAfterStart = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Equal(TokenPhaseStep.RecycleChoosingReplacement, tokenPhaseAfterStart.Step);
        Assert.Equal(new[] { TokenAction.Steal }, tokenPhaseAfterStart.RecycleReplacementOptions);

        var picked = session.TryTokenPhaseRecyclePick(0, TokenAction.Steal, out var pickError, out var pickFizzle);

        Assert.True(picked, pickError);
        Assert.Equal(TokenAction.Recycle, pickFizzle);

        var tokenPhaseAfterFizzle = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Null(tokenPhaseAfterFizzle.ActiveToken);
        Assert.Equal(TokenPhaseStep.ChoosingNextToken, tokenPhaseAfterFizzle.Step);
        Assert.Equal(
            new[] { TokenAction.StashTrash, TokenAction.DoubleStash, TokenAction.DoubleTrash, TokenAction.Bandit, TokenAction.Steal }
                .OrderBy(t => t),
            tokenPhaseAfterFizzle.RemainingTokens.OrderBy(t => t));

        var log = session.GetViewForPlayer(0).Log;
        Assert.Contains(log, e => e.Message.Contains("picked a Recycle token, but there was no unrolled token to swap in"));
    }

    [Fact]
    public void Recycle_ZeroOptions_SurfacesFizzleTokenFromApplyAction()
    {
        var die = DieMockFactory.CreateSequenced(
            TokenAction.StashTrash, TokenAction.DoubleStash, TokenAction.DoubleTrash,
            TokenAction.Bandit, TokenAction.Steal, TokenAction.Recycle).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (_, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 6);

        var succeeded = session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var error, out var resolvedWithNoEffectToken);

        // The HTTP layer builds its InfoMessage entirely off this out-parameter (see
        // GameApplicationService.BuildTokenFizzleInfoMessage) — this asserts the signal reaches all
        // the way out to GameSession.ApplyAction's own caller, not just the game log.
        Assert.True(succeeded, error);
        Assert.Equal(TokenAction.Recycle, resolvedWithNoEffectToken);
    }

    // --- B2: StashTrash stash-mode with zero stashable cards ----------------------------------------

    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInStashTrashChooseBranch(
        Die die, IDrawPile drawPile)
    {
        var (p0, p1, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 1);
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenStashTrash, die, out var error, out _), error);
        Assert.Equal(TokenPhaseStep.StashTrashChooseBranch, session.GetViewForPlayer(0).TokenPhase!.Step);
        return (p0, p1, session);
    }

    [Fact]
    public void StashTrash_WithOnlyDoggoAndKittehInHand_DoesNotOfferStashMode()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var p0 = new Player(0, "Alice");
        p0.Hand.Add(new Card(CardName.Doggo));
        p0.Hand.Add(new Card(CardName.Kitteh));
        var (_, _, session) = CreateApiModeSessionInStashTrashChooseBranchWithPlayer(p0, die, drawPile);

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.DoesNotContain(GameAction.TokenStashTrashStashMode, allowed);
        Assert.Contains(GameAction.TokenStashTrashDrawOne, allowed);
    }

    [Fact]
    public void StashTrash_WithEmptyHand_DoesNotOfferStashMode()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (_, _, session) = CreateApiModeSessionInStashTrashChooseBranch(die, drawPile);
        Assert.Empty(session.CurrentPlayer.Hand);

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.DoesNotContain(GameAction.TokenStashTrashStashMode, allowed);
        Assert.Contains(GameAction.TokenStashTrashDrawOne, allowed);
    }

    [Fact]
    public void StashTrash_WithAStashableCard_StillOffersStashMode()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var p0 = new Player(0, "Alice");
        p0.Hand.Add(new Card(CardName.Feesh));
        var (_, _, session) = CreateApiModeSessionInStashTrashChooseBranchWithPlayer(p0, die, drawPile);

        var allowed = session.GetAllowedActionsForPlayer(0);

        Assert.Contains(GameAction.TokenStashTrashStashMode, allowed);
        Assert.Contains(GameAction.TokenStashTrashDrawOne, allowed);
    }

    [Fact]
    public void StashTrash_EnterStashMode_RejectedWhenNothingCanBeStashed()
    {
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        p0.Hand.Add(new Card(CardName.Doggo));
        p0.Hand.Add(new Card(CardName.Kitteh));
        var session = new GameSession(new[] { p0, p1 }, DrawPileMockFactory.CreateWithCards(5).Object);

        // Bypasses GameSession.ApplyAction's own allowed-actions gate on purpose, to exercise
        // TryStashTrashEnterStashMode's defensive guard directly, as if a raw command had been
        // submitted for an action outside the current allowedActions list.
        var coordinator = new TokenPhaseCoordinator(session);
        coordinator.Begin(new[] { TokenAction.StashTrash });
        Assert.True(coordinator.TryApplyGameAction(0, GameAction.ResolveTokenStashTrash, out var startError, out _), startError);

        var succeeded = coordinator.TryApplyGameAction(0, GameAction.TokenStashTrashStashMode, out var error, out var resolvedWithNoEffectToken);

        Assert.False(succeeded);
        Assert.NotNull(error);
        Assert.Null(resolvedWithNoEffectToken);
    }

    [Fact]
    public void StashTrash_StashModeGateMatchesPromptList()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var p0 = new Player(0, "Alice");
        // NewlyAdded is true here on purpose: the anti-drift assertion for this test only holds if the
        // stash-mode gate (TokenPhaseAllowedActionsProvider.HasStashableHandCard) matches
        // TokenPhaseViewBuilder.GetStashableHandTuplesForView exactly — neither filters on NewlyAdded.
        // If someone later adds a NewlyAdded condition to only one side, this card would make the gate
        // and the prompt list disagree, and this test would fail.
        p0.Hand.Add(new Card(CardName.Feesh), newlyAdded: true);
        var (_, _, session) = CreateApiModeSessionInStashTrashChooseBranchWithPlayer(p0, die, drawPile);

        Assert.Contains(GameAction.TokenStashTrashStashMode, session.GetAllowedActionsForPlayer(0));

        Assert.True(session.ApplyAction(0, GameAction.TokenStashTrashStashMode, die, out var error, out _), error);

        Assert.NotEmpty(session.GetViewForPlayer(0).TokenPhase!.StashableHandCardsForCurrentPrompt);
    }

    private static (Player p0, Player p1, GameSession session) CreateApiModeSessionInStashTrashChooseBranchWithPlayer(
        Player p0, Die die, IDrawPile drawPile)
    {
        var p1 = new Player(1, "Bob");
        var session = new GameSession(new[] { p0, p1 }, drawPile);

        session.ApplyAction(0, GameAction.RollDie, die, out _, out _);
        Assert.Single(session.PhaseOne.Tokens);
        session.ApplyAction(0, GameAction.StopRolling, die, out _, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _, out _);
        Assert.Equal(GameState.TokenPhase, session.State);

        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenStashTrash, die, out var error, out _), error);
        Assert.Equal(TokenPhaseStep.StashTrashChooseBranch, session.GetViewForPlayer(0).TokenPhase!.Step);

        return (p0, p1, session);
    }

    // --- B3: Recycle repeat picking the same replacement twice --------------------------------------

    [Fact]
    public void MmmPieRepeatOfRecycle_CannotPickTheSameReplacementTwice()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.DoubleTrash, TokenAction.Recycle).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 2);
        p0.Hand.Add(new Card(CardName.MmmPie));

        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var startError, out _), startError);

        var firstOptions = session.GetViewForPlayer(0).TokenPhase!.RecycleReplacementOptions;
        Assert.Equal(
            new[] { TokenAction.StashTrash, TokenAction.DoubleStash, TokenAction.Bandit, TokenAction.Steal }.OrderBy(t => t),
            firstOptions.OrderBy(t => t));

        var firstPicked = session.TryTokenPhaseRecyclePick(0, TokenAction.StashTrash, out var firstPickError, out var firstPickFizzle);
        Assert.True(firstPicked, firstPickError);
        Assert.Null(firstPickFizzle);

        var tokenPhaseAfterFirstPick = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Equal(TokenPhaseStep.RecycleChoosingReplacement, tokenPhaseAfterFirstPick.Step);
        Assert.Equal(
            new[] { TokenAction.DoubleStash, TokenAction.Bandit, TokenAction.Steal }.OrderBy(t => t),
            tokenPhaseAfterFirstPick.RecycleReplacementOptions.OrderBy(t => t));
        Assert.DoesNotContain(TokenAction.StashTrash, tokenPhaseAfterFirstPick.RecycleReplacementOptions);
        Assert.DoesNotContain(TokenAction.DoubleTrash, tokenPhaseAfterFirstPick.RecycleReplacementOptions);
        Assert.DoesNotContain(TokenAction.Recycle, tokenPhaseAfterFirstPick.RecycleReplacementOptions);

        var secondAttempt = session.TryTokenPhaseRecyclePick(0, TokenAction.StashTrash, out var secondError, out _);

        Assert.False(secondAttempt);
        Assert.NotNull(secondError);
    }

    [Fact]
    public void Recycle_PickedReplacement_ExcludedFromASecondRecycleWithinTheSameChain()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.DoubleTrash, TokenAction.Recycle).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (p0, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 2);
        p0.Hand.Add(new Card(CardName.MmmPie));

        Assert.True(session.ApplyAction(0, GameAction.PlayMmmPieTokenPhase, die, out var mmmPieError, out _), mmmPieError);
        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var startError, out _), startError);

        Assert.True(session.TryTokenPhaseRecyclePick(0, TokenAction.StashTrash, out var firstPickError, out var firstPickFizzle), firstPickError);
        Assert.Null(firstPickFizzle);

        // Second pick of the same MmmPie-repeated chain — verified via GameSession's own
        // GetTokenPhaseRecycleOptions() delegation, a different API surface than the view used above.
        var secondOptions = session.GetTokenPhaseRecycleOptions();
        Assert.Equal(
            new[] { TokenAction.DoubleStash, TokenAction.Bandit, TokenAction.Steal }.OrderBy(t => t),
            secondOptions.OrderBy(t => t));

        var secondPicked = session.TryTokenPhaseRecyclePick(0, TokenAction.Bandit, out var secondPickError, out var secondPickFizzle);
        Assert.True(secondPicked, secondPickError);
        Assert.Null(secondPickFizzle);

        var tokenPhase = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Equal(TokenPhaseStep.ChoosingNextToken, tokenPhase.Step);
        Assert.Equal(
            new[] { TokenAction.DoubleTrash, TokenAction.StashTrash, TokenAction.Bandit }.OrderBy(t => t),
            tokenPhase.RemainingTokens.OrderBy(t => t));
    }

    [Fact]
    public void Recycle_RejectedPick_DoesNotPolluteExclusionSet()
    {
        var die = DieMockFactory.CreateSequenced(TokenAction.Recycle, TokenAction.StashTrash, TokenAction.DoubleTrash).Object;
        var drawPile = DrawPileMockFactory.CreateWithCards(10).Object;
        var (_, _, session) = CreateApiModeSessionInTokenPhase(die, drawPile, rollCount: 3);

        Assert.True(session.ApplyAction(0, GameAction.ResolveTokenRecycle, die, out var startError, out _), startError);

        var optionsBeforeRejectedAttempt = session.GetTokenPhaseRecycleOptions();
        Assert.Equal(
            new[] { TokenAction.DoubleStash, TokenAction.Bandit, TokenAction.Steal }.OrderBy(t => t),
            optionsBeforeRejectedAttempt.OrderBy(t => t));

        // Invalid: StashTrash was already rolled at the start of TokenPhase, so it's already ineligible.
        var rejected = session.TryTokenPhaseRecyclePick(0, TokenAction.StashTrash, out var rejectedError, out var rejectedFizzle);
        Assert.False(rejected);
        Assert.NotNull(rejectedError);
        Assert.Null(rejectedFizzle);

        var optionsAfterRejectedAttempt = session.GetTokenPhaseRecycleOptions();
        Assert.Equal(
            optionsBeforeRejectedAttempt.OrderBy(t => t),
            optionsAfterRejectedAttempt.OrderBy(t => t));

        var validPick = session.TryTokenPhaseRecyclePick(0, TokenAction.Bandit, out var validPickError, out var validPickFizzle);
        Assert.True(validPick, validPickError);
        Assert.Null(validPickFizzle);

        var tokenPhase = session.GetViewForPlayer(0).TokenPhase!;
        Assert.Equal(
            new[] { TokenAction.StashTrash, TokenAction.DoubleTrash, TokenAction.Bandit }.OrderBy(t => t),
            tokenPhase.RemainingTokens.OrderBy(t => t));
    }
}
