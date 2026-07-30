namespace TrashAnimal.TokenPhase;

/// <summary>
/// Owns "finish the active token pass, or repeat it once for MmmPie" — the completion/repeat side of
/// token resolution. Split out from <see cref="TokenPhaseTokenResolver"/> (which owns "start a token")
/// to keep both files under this repo's file-length guidance. Implements <see cref="ITokenPhaseTokenCompletion"/>
/// so <see cref="TokenPhaseBanditHandler"/> can trigger completion without depending on the resolver directly.
///
/// Any token that can't produce its normal effect on a repeat (Steal with no hand candidates, Bandit with
/// an empty deck) auto-resolves with a <see cref="GameLog.TokenResolvedWithNoEffectEvent"/> instead of
/// leaving the token stuck, and surfaces that fizzle via <paramref name="resolvedWithNoEffectToken"/> so
/// callers can report it (e.g. as an HTTP response's InfoMessage) regardless of which entry point triggered
/// the repeat.
/// </summary>
internal sealed class TokenPhaseTokenCompletionEngine : ITokenPhaseTokenCompletion
{
    private readonly GameSession _session;
    private readonly TokenPhaseBanditHandler _bandit;
    private readonly TokenPhaseStealHandler _steal;
    private readonly TokenPhaseViewBuilder _viewBuilder;

    public TokenPhaseTokenCompletionEngine(
        GameSession session,
        TokenPhaseBanditHandler bandit,
        TokenPhaseStealHandler steal,
        TokenPhaseViewBuilder viewBuilder)
    {
        _session = session;
        _bandit = bandit;
        _steal = steal;
        _viewBuilder = viewBuilder;
    }

    public bool TryFinishCurrentTokenPassOrRepeat(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;

        // ResolveTokenTwice (set by MmmPie) must be cleared unconditionally here, not only on the actual-repeat
        // branch below. A token that fizzles via the zero-effect path never sets ActiveToken, so the repeat
        // condition would be false and this flag would otherwise survive into the next token's resolution and
        // incorrectly double-resolve it.
        var repeatToken = state.ResolveTokenTwice ? state.ActiveToken : null;
        state.ResolveTokenTwice = false;

        if (repeatToken is { } activeForRepeat)
            return RestartSubflow(activeForRepeat, state, out error, out resolvedWithNoEffectToken);

        state.ActiveToken = null;
        state.Step = TokenPhaseStep.ChoosingNextToken;
        state.ResetBanditWindow();

        if (state.RemainingTokens.Count == 0)
            _session.CompleteTokenPhaseAndEndTurn();

        return true;
    }

    /// <summary>Runs a DoubleTrash draw (start or repeat) then immediately finishes/repeats the pass. Shared by
    /// <see cref="TokenPhaseTokenResolver.TryStartToken"/> and this engine's own repeat branch so the
    /// draw-then-finish sequence has one definition.</summary>
    public bool TryRunDoubleTrashAndFinish(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        var drawn = _session.DrawPile.DealCards(2).ToList();
        _session.CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);
        _session.RegisterDrawOutcome(drawn);
        TokenPhaseTokenLogRecording.RecordCardsDrawnPrivately(_session, drawn);
        return TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
    }

    private bool RestartSubflow(TokenAction token, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        state.ResetBanditWindow();
        TokenPhaseTokenLogRecording.RecordTokenResolutionStarted(_session, token);

        switch (token)
        {
            case TokenAction.StashTrash:
                state.Step = TokenPhaseStep.StashTrashChooseBranch;
                return true;

            case TokenAction.DoubleStash:
                state.Step = TokenPhaseStep.DoubleStashChoosingCards;
                return true;

            case TokenAction.DoubleTrash:
                return TryRunDoubleTrashAndFinish(state, out error, out resolvedWithNoEffectToken);

            case TokenAction.Bandit:
                if (!_bandit.StartBandit(state, out error))
                {
                    state.ActiveToken = null;
                    TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
                    var finished = TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
                    resolvedWithNoEffectToken = token;
                    return finished;
                }

                return true;

            case TokenAction.Steal:
                if (!_steal.HasCandidates())
                {
                    state.ActiveToken = null;
                    TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
                    var finished = TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
                    resolvedWithNoEffectToken = token;
                    return finished;
                }

                // Server-initiated decision point: unlike the first pick, no client request is in flight that
                // could have carried a victim, so park in an explicit step and wait to be asked again.
                state.Step = TokenPhaseStep.StealChoosingVictim;
                return true;

            case TokenAction.Recycle:
                if (_viewBuilder.GetRecycleOptions(state).Count == 0)
                {
                    state.ActiveToken = null;
                    TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
                    var finished = TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
                    resolvedWithNoEffectToken = token;
                    return finished;
                }

                state.Step = TokenPhaseStep.RecycleChoosingReplacement;
                return true;

            default:
                error = "Unsupported token for repeat.";
                return false;
        }
    }
}
