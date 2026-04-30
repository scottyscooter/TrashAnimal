using TrashAnimal.RollPhase;

namespace TrashAnimal;

public sealed partial class GameSession
{
    private RollPhaseOfferSnapshot CreateRollPhaseOfferSnapshot(bool isBustedBranch) => new(
        IsBustedBranch: isBustedBranch,
        CurrentPlayer: CurrentPlayer,
        Players: _players,
        CurrentPlayerIndex: CurrentPlayerIndex,
        DiscardPileCount: DiscardPile.Count,
        HasFeeshSelector: OnFeeshCardSelection is not null,
        HasShinyVictimSelector: ChooseShinyStealVictim is not null);

    private RollPhasePlayContext CreateRollPhasePlayContext() => new()
    {
        Players = _players,
        CurrentPlayerIndex = CurrentPlayerIndex,
        PhaseOne = PhaseOne,
        DiscardPile = DiscardPile,
        Steal = _steal,
        CurrentState = State,
        IsPhaseOneActive = IsPhaseOneActive,
        OnFeeshCardSelection = OnFeeshCardSelection,
        ChooseShinyStealVictim = ChooseShinyStealVictim,
        ApplyState = s => State = s,
        ApplyCanRoll = v => _canRoll = v,
        ApplyHasStoppedRolling = v => _hasStoppedRolling = v,
        OnStashStealBegun = () => ArmStealResumeState(GameState.RollPhase)
    };

    private bool TryExecuteRollPhaseHandler(GameAction action, int playerIndex, out string? error)
    {
        if (!_rollPhaseHandlers.TryGetHandler(action, out var handler) || handler is null)
        {
            error = "Unknown roll-phase action.";
            return false;
        }

        return handler.TryExecute(CreateRollPhasePlayContext(), playerIndex, out error);
    }

    public bool TryStealPass(int victimIndex, out string? error)
    {
        EnsureState(GameState.AwaitingStealResponse);
        if (!_steal.TryRefuseToBlockSteal(victimIndex, out var aftermath, out error))
            return false;

        if (aftermath == StealAttemptAftermath.AwaitingCardPick)
            State = GameState.AwaitingStealCardPick;

        return true;
    }

    public bool TryStealPlayDoggo(int victimIndex, out string? error)
    {
        EnsureState(GameState.AwaitingStealResponse);
        var wasStealToken = _tokenPhaseCoordinator.IsActive && _tokenPhaseCoordinator.ActiveTokenIsSteal;
        if (!_steal.TryPlayDoggo(victimIndex, _players, DiscardPile, _drawPile, CurrentPlayerIndex, out var aftermath, out error))
            return false;

        if (aftermath == StealAttemptAftermath.Completed)
        {
            State = StealResumeState;
            ResetStealResumeStateToRollPhase();
            if (State == GameState.TokenPhase && _tokenPhaseCoordinator.IsActive)
                _tokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase(wasStealToken);
        }

        return true;
    }

    public bool TryStealPlayKitteh(int victimIndex, out string? error)
    {
        EnsureState(GameState.AwaitingStealResponse);
        return _steal.TryPlayKitteh(victimIndex, _players, DiscardPile, out error);
    }

    public bool TryCompleteStealWithCard(int thiefIndex, Guid cardId, out string? error)
    {
        EnsureState(GameState.AwaitingStealCardPick);
        var wasStealToken = _tokenPhaseCoordinator.IsActive && _tokenPhaseCoordinator.ActiveTokenIsSteal;
        if (!_steal.TryCompletePick(thiefIndex, cardId, _players, CurrentPlayerIndex, out error))
            return false;

        State = StealResumeState;
        ResetStealResumeStateToRollPhase();
        if (State == GameState.TokenPhase && _tokenPhaseCoordinator.IsActive)
            _tokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase(wasStealToken);

        return true;
    }

    public bool TryYumYumRespond(int opponentPlayerIndex, bool playYumYum, out string? error)
    {
        EnsureState(GameState.AwaitingYumYum);
        return _yumYumWindow.TryRespond(
            opponentPlayerIndex,
            playYumYum,
            _players,
            DiscardPile,
            PhaseOne,
            onYumYumPlayedAllowRollsAgain: () => _hasStoppedRolling = false,
            onWindowClosedReturnToRollPhase: () => State = GameState.RollPhase,
            out error);
    }

    public bool TryRecoverFromBustWithNanners(out string? error) =>
        TryExecuteRollPhaseHandler(GameAction.PlayNanners, CurrentPlayerIndex, out error);

    public bool TryRecoverFromBustWithBlammo(out string? error) =>
        TryExecuteRollPhaseHandler(GameAction.PlayBlammo, CurrentPlayerIndex, out error);

    private RollResult RollDie(Die die)
    {
        EnsureState(GameState.RollPhase);
        if (_yumYumWindow.IsAwaiting)
            throw new InvalidOperationException("Resolve the Yum Yum window before rolling.");

        return PhaseOne.TryRollForToken(die);
    }

    private bool TryRequestVoluntaryStop(out string? error)
    {
        error = null;
        EnsureState(GameState.RollPhase);
        if (!IsPhaseOneActive)
        {
            error = "RollPhase is not active.";
            return false;
        }

        if (_yumYumWindow.IsAwaiting)
        {
            error = "Already awaiting Yum Yum responses.";
            return false;
        }

        if (!PhaseOne.CanVoluntarilyStop())
        {
            error = "Cannot stop while busted or forced rolls remain.";
            return false;
        }

        _hasStoppedRolling = true;

        _yumYumWindow.Open(GetOpponentIndicesClockwise(CurrentPlayerIndex, _players.Count));
        State = GameState.AwaitingYumYum;
        return true;
    }
}
