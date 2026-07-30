using TrashAnimal.GameLog;
using TrashAnimal.RollPhase;

namespace TrashAnimal;

public sealed partial class GameSession
{
    private RollPhaseOfferSnapshot CreateRollPhaseOfferSnapshot(bool isBustedBranch) => new(
        IsBustedBranch: isBustedBranch,
        CurrentPlayer: CurrentPlayer,
        Players: _players,
        CurrentPlayerIndex: CurrentPlayerIndex,
        DiscardPileCount: ComputeFeeshEligibleDiscardCount(),
        HasFeeshSelector: true,
        HasShinyVictimSelector: true);

    private int ComputeFeeshEligibleDiscardCount() =>
        _bustRecoveryCardDiscardedId.HasValue
            ? DiscardPile.Count(c => c.Id != _bustRecoveryCardDiscardedId.Value)
            : DiscardPile.Count;

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
        OnStashStealBegun = () => ArmStealResumeState(GameState.RollPhase),
        NotifyBustRecoveryCardDiscarded = id => _bustRecoveryCardDiscardedId = id
    };

    private bool TryExecuteRollPhaseHandler(GameAction action, int playerIndex, out string? error)
    {
        if (!_rollPhaseHandlers.TryGetHandler(action, out var handler) || handler is null)
        {
            error = "Unknown roll-phase action.";
            return false;
        }

        var handCardIdsBefore = action == GameAction.PlayFeesh
            ? _players[playerIndex].Hand.Select(e => e.Card.Id).ToHashSet()
            : null;

        if (!handler.TryExecute(CreateRollPhasePlayContext(), playerIndex, out error))
            return false;

        RecordLogEvent(BuildRollPhaseHandlerLogEvent(action, playerIndex, handCardIdsBefore));
        return true;
    }

    private GameLogEvent BuildRollPhaseHandlerLogEvent(GameAction action, int playerIndex, HashSet<Guid>? handCardIdsBefore)
    {
        switch (action)
        {
            case GameAction.PlayFeesh:
                var retrieved = _players[playerIndex].Hand.FirstOrDefault(e => !handCardIdsBefore!.Contains(e.Card.Id));
                var card = retrieved?.Card;
                return RollPhaseLogEventFactory.ForFeeshRetrieved(
                    playerIndex, TurnNumber, card?.Id ?? Guid.Empty, card?.Name ?? CardName.Feesh);

            case GameAction.PlayShiny:
                return RollPhaseLogEventFactory.ForShinyStealBegun(playerIndex, TurnNumber, _steal.VictimIndex!.Value);

            case GameAction.PlayNanners:
                return RollPhaseLogEventFactory.ForBustRecoveryCardPlayed(playerIndex, TurnNumber, CardName.Nanners);

            case GameAction.PlayBlammo:
                return RollPhaseLogEventFactory.ForBustRecoveryCardPlayed(playerIndex, TurnNumber, CardName.Blammo);

            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported RollPhase handler action for game log emission.");
        }
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

    public bool TryStealPlayDoggo(int victimIndex, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        EnsureState(GameState.AwaitingStealResponse);
        var wasStealToken = _tokenPhaseCoordinator.IsActive && _tokenPhaseCoordinator.ActiveTokenIsSteal;
        var thiefIndex = _steal.ThiefIndex;
        if (!_steal.TryPlayDoggo(
                victimIndex,
                _players,
                DiscardPile,
                _drawPile,
                CurrentPlayerIndex,
                out var drawnFromPile,
                out var aftermath,
                out error))
            return false;

        RegisterDrawOutcome(drawnFromPile);

        if (aftermath == StealAttemptAftermath.Completed)
        {
            RecordLogEvent(new StealBlockedEvent(0, TurnNumber, victimIndex, thiefIndex!.Value, CardName.Doggo));

            State = StealResumeState;
            ResetStealResumeStateToRollPhase();
            if (State == GameState.TokenPhase && _tokenPhaseCoordinator.IsActive
                && !_tokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase(wasStealToken, out error, out resolvedWithNoEffectToken))
                return false;
        }

        return true;
    }

    public bool TryStealPlayKitteh(int victimIndex, out string? error)
    {
        EnsureState(GameState.AwaitingStealResponse);
        if (!_steal.TryPlayKitteh(victimIndex, _players, DiscardPile, out error))
            return false;

        RecordLogEvent(new StealRoleSwappedEvent(0, TurnNumber, victimIndex, _steal.VictimIndex!.Value));
        return true;
    }

    public bool TryCompleteStealWithCard(int thiefIndex, Guid cardId, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        EnsureState(GameState.AwaitingStealCardPick);
        var wasStealToken = _tokenPhaseCoordinator.IsActive && _tokenPhaseCoordinator.ActiveTokenIsSteal;
        var victimIndex = _steal.VictimIndex!.Value;
        var zone = _steal.InitialStealTargetZone!.Value;
        var stolenCardName = FindCardName(victimIndex, zone, cardId);

        if (!_steal.TryCompletePick(thiefIndex, cardId, _players, CurrentPlayerIndex, out error))
            return false;

        RecordLogEvent(new StealCompletedEvent(0, TurnNumber, thiefIndex, victimIndex, zone, cardId, stolenCardName));

        State = StealResumeState;
        ResetStealResumeStateToRollPhase();
        if (State == GameState.TokenPhase && _tokenPhaseCoordinator.IsActive
            && !_tokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase(wasStealToken, out error, out resolvedWithNoEffectToken))
            return false;

        return true;
    }

    private CardName FindCardName(int victimIndex, StealTargetZone zone, Guid cardId)
    {
        var victim = _players[victimIndex];
        CardName? entry = zone == StealTargetZone.Stash
            ? victim.StashPile.FirstOrDefault(e => e.Card.Id == cardId)?.Card.Name
            : victim.Hand.FirstOrDefault(e => e.Card.Id == cardId)?.Card.Name;
        return entry ?? throw new InvalidOperationException("Card to be stolen was not found in the victim's zone.");
    }

    public bool TryYumYumRespond(int opponentPlayerIndex, bool playYumYum, out string? error)
    {
        EnsureState(GameState.AwaitingYumYum);
        var rollerIndex = CurrentPlayerIndex;
        return _yumYumWindow.TryRespond(
            opponentPlayerIndex,
            playYumYum,
            _players,
            DiscardPile,
            PhaseOne,
            onYumYumPlayedAllowRollsAgain: () =>
            {
                _hasStoppedRolling = false;
                RecordLogEvent(new YumYumForcedRerollEvent(0, TurnNumber, rollerIndex, opponentPlayerIndex));
            },
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
        RecordLogEvent(new TurnStoppedRollingEvent(0, TurnNumber, CurrentPlayerIndex));
        return true;
    }
}
