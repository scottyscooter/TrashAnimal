using TrashAnimal.RollPhase;

namespace TrashAnimal;

public sealed class GameSession
{
    private readonly IPhaseTwo _phaseTwo;
    private readonly IDrawPile _drawPile;
    private readonly RollPhaseGameplayHandlerRegistry _rollPhaseHandlers =
        new(RollPhaseGameplayHandlers.CreateDefault());
    private readonly List<Player> _players;
    private readonly YumYumWindow _yumYumWindow = new();
    private bool _canRoll;
    private bool _hasStoppedRolling;

    private readonly StealAttempt _steal = new();

    public GameSession(IReadOnlyList<Player> players, IPhaseTwo phaseTwo, IDrawPile drawPile)
    {
        if (players.Count is < 2 or > 4)
            throw new ArgumentException("Player count must be between 2 and 4.", nameof(players));

        _phaseTwo = phaseTwo ?? throw new ArgumentNullException(nameof(phaseTwo));
        _drawPile = drawPile ?? throw new ArgumentNullException(nameof(drawPile));
        _players = new List<Player>(players);
        CurrentPlayerIndex = 0;
        BeginTurn();
    }

    public IReadOnlyList<Player> Players => _players;
    public int CurrentPlayerIndex { get; private set; }
    public Player CurrentPlayer => _players[CurrentPlayerIndex];
    public PhaseOneState PhaseOne { get; } = new();
    public List<Card> DiscardPile { get; } = new();

    public GameState State { get; private set; } = GameState.RollPhase;
    public IReadOnlyList<TokenAction> LastPhaseTwoTokens { get; private set; } = Array.Empty<TokenAction>();

    /// <summary>True while the active player may take RollPhase actions (including after a bust until resolved or abandoned).</summary>
    public bool IsPhaseOneActive { get; private set; }

    /// <summary>After a voluntary stop, before TokenPhase: opponents respond in clockwise order; at most one Yum Yum per stop.</summary>
    public bool AwaitingYumYumWindow => _yumYumWindow.IsAwaiting;

    /// <summary>Optional hook when Feesh is played.</summary>
    public Action<int>? OnFeeshPlayed { get; set; }

    /// <summary>Selects a specific card from discard when Feesh is played.</summary>
    public Func<int, IReadOnlyList<Card>, Card?>? OnFeeshCardSelection { get; set; }

    /// <summary>Selects victim index when Shiny is played; candidates are opponents with at least one stashed card.</summary>
    public Func<int, IReadOnlyList<int>, int>? ChooseShinyStealVictim { get; set; }

    public int? StealThiefIndex => _steal.ThiefIndex;
    public int? StealVictimIndex => _steal.VictimIndex;
    public StealTargetZone? InitialStealTargetZone => _steal.InitialStealTargetZone;

    public void BeginTurn()
    {
        ClearStealChain();
        IsPhaseOneActive = true;
        _yumYumWindow.Reset();
        PhaseOne.Reset();
        _canRoll = true;
        _hasStoppedRolling = false;
        LastPhaseTwoTokens = Array.Empty<TokenAction>();
        State = GameState.RollPhase;
    }

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

    public IReadOnlyList<GameAction> GetAllowedActionsForPlayer(int playerIndex)
    {
        if (State == GameState.AwaitingStealResponse)
        {
            if (playerIndex != _steal.VictimIndex)
                return Array.Empty<GameAction>();

            return _steal.GetAllowedResponseActions(_players[playerIndex]);
        }

        if (State == GameState.AwaitingStealCardPick)
            return Array.Empty<GameAction>();

        if (State == GameState.AwaitingYumYum)
        {
            var responder = GetCurrentYumYumResponderIndex();
            if (responder == playerIndex)
            {
                var hasYum = _players[playerIndex].Hand.Any(c => c.Name == CardName.Yumyum);
                return hasYum
                    ? new[] { GameAction.YumYumPlay, GameAction.YumYumPass }
                    : new[] { GameAction.YumYumPass };
            }

            return Array.Empty<GameAction>();
        }

        if (playerIndex != CurrentPlayerIndex)
            return Array.Empty<GameAction>();

        if (State == GameState.TurnEnd)
            return new[] { GameAction.EndTurn };

        if (State != GameState.RollPhase)
            return Array.Empty<GameAction>();

        var rollActions = new List<GameAction>();
        var snapshot = CreateRollPhaseOfferSnapshot(PhaseOne.IsBusted);

        if (!PhaseOne.IsBusted)
        {
            if ((_canRoll && !_hasStoppedRolling) || PhaseOne.ForcedRollRemaining)
            {
                rollActions.Add(GameAction.RollDie);
                if (PhaseOne.CanVoluntarilyStop())
                    rollActions.Add(GameAction.StopRolling);
            }

            foreach (var handler in _rollPhaseHandlers.All)
            {
                if (handler.IsActionable(in snapshot))
                    rollActions.Add(handler.Action);
            }

            if ((!_canRoll || _hasStoppedRolling) && !PhaseOne.ForcedRollRemaining)
                rollActions.Add(GameAction.AdvanceToResolveTokens);
        }
        else
        {
            foreach (var handler in _rollPhaseHandlers.All)
            {
                if (handler.IsActionable(in snapshot))
                    rollActions.Add(handler.Action);
            }

            rollActions.Add(GameAction.AbandonBust);
        }

        return rollActions;
    }

    public bool ApplyAction(int playerIndex, GameAction action, Die die, out string? error)
    {
        error = null;

        var allowed = GetAllowedActionsForPlayer(playerIndex);
        if (!allowed.Contains(action))
        {
            error = "Action is not allowed right now.";
            return false;
        }

        switch (action)
        {
            case GameAction.RollDie:
                RollDie(die);
                return true;

            case GameAction.StopRolling:
                return TryRequestVoluntaryStop(out error);

            case GameAction.AdvanceToResolveTokens:
                return TryAdvanceToResolveTokens(out error);

            case GameAction.PlayShiny:
            case GameAction.PlayFeesh:
            case GameAction.PlayNanners:
            case GameAction.PlayBlammo:
                return TryExecuteRollPhaseHandler(action, playerIndex, out error);

            case GameAction.AbandonBust:
                return TryAdvanceToResolveTokens(out error);

            case GameAction.YumYumPlay:
                return TryYumYumRespond(playerIndex, playYumYum: true, out error);

            case GameAction.YumYumPass:
                return TryYumYumRespond(playerIndex, playYumYum: false, out error);

            case GameAction.StealPass:
                return TryStealPass(playerIndex, out error);

            case GameAction.StealPlayDoggo:
                return TryStealPlayDoggo(playerIndex, out error);

            case GameAction.StealPlayKitteh:
                return TryStealPlayKitteh(playerIndex, out error);

            case GameAction.EndTurn:
                EndTurn();
                return true;

            default:
                error = "Unknown action.";
                return false;
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

    public bool TryStealPlayDoggo(int victimIndex, out string? error)
    {
        EnsureState(GameState.AwaitingStealResponse);
        if (!_steal.TryPlayDoggo(victimIndex, _players, DiscardPile, _drawPile, out var aftermath, out error))
            return false;

        if (aftermath == StealAttemptAftermath.Completed)
            State = GameState.RollPhase;

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
        if (!_steal.TryCompletePick(thiefIndex, cardId, _players, out error))
            return false;

        State = GameState.RollPhase;
        return true;
    }

    /// <summary>Opponent at the current response slot passes or plays Yum Yum (at most one play per stop).</summary>
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

    /// <summary>Nanners: clear bust and stop further rolling this phase.</summary>
    public bool TryRecoverFromBustWithNanners(out string? error) =>
        TryExecuteRollPhaseHandler(GameAction.PlayNanners, CurrentPlayerIndex, out error);

    /// <summary>Blammo: clear bust and require one forced roll.</summary>
    public bool TryRecoverFromBustWithBlammo(out string? error) =>
        TryExecuteRollPhaseHandler(GameAction.PlayBlammo, CurrentPlayerIndex, out error);

    public bool TryAdvanceToResolveTokens(out string? error)
    {
        error = null;
        EnsureState(GameState.RollPhase);

        if (PhaseOne.IsBusted)
            // Bust with no Nanners/Blammo recovery — TokenPhase receives no tokens
            GoToPhaseTwo(CurrentPlayerIndex, Array.Empty<TokenAction>());
        else
            GoToPhaseTwo(CurrentPlayerIndex, PhaseOne.Tokens);

        return true;
    }

    public bool TryPlayShinyOrFeesh(int playerIndex, CardName cardName, out string? error)
    {
        error = null;
        if (cardName is not (CardName.Shiny or CardName.Feesh))
        {
            error = "Only Shiny or Feesh may be played with this action.";
            return false;
        }

        var action = cardName == CardName.Shiny ? GameAction.PlayShiny : GameAction.PlayFeesh;
        return TryExecuteRollPhaseHandler(action, playerIndex, out error);
    }

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
        OnFeeshPlayed = OnFeeshPlayed,
        ApplyState = s => State = s,
        ApplyCanRoll = v => _canRoll = v,
        ApplyHasStoppedRolling = v => _hasStoppedRolling = v
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

    public GameView GetViewForPlayer(int playerIndex)
    {
        var responderIndex = GetCurrentYumYumResponderIndex();
        var responderName = responderIndex is null ? null : _players[responderIndex.Value].Name;

        var hand = _players[playerIndex].Hand.Select(c => c.Name).ToList();

        var stealPhase = _steal.BuildPhaseView(State, playerIndex, _players);

        return new GameView(
            State,
            CurrentPlayerIndex,
            CurrentPlayer.Name,
            PhaseOne.IsBusted,
            PhaseOne.ForcedRollRemaining,
            PhaseOne.Tokens,
            hand,
            responderIndex,
            responderName,
            stealPhase);
    }

    public void EndTurn()
    {
        if (State != GameState.TurnEnd)
            throw new InvalidOperationException("Turn cannot end until TokenPhase has completed.");

        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        BeginTurn();
    }

    public int? GetCurrentYumYumResponderIndex() => _yumYumWindow.GetCurrentResponderPlayerIndex();

    private void GoToPhaseTwo(int playerIndex, IReadOnlyList<TokenAction> tokens)
    {
        IsPhaseOneActive = false;
        _yumYumWindow.Reset();
        ClearStealChain();
        _hasStoppedRolling = true;
        State = GameState.TokenPhase;
        _phaseTwo.ResolvePhaseTwo(playerIndex, tokens);
        LastPhaseTwoTokens = tokens.ToList();
        State = GameState.TurnEnd;
    }

    public static IEnumerable<int> GetOpponentIndicesClockwise(int currentPlayerIndex, int playerCount)
    {
        for (var step = 1; step < playerCount; step++)
            yield return (currentPlayerIndex + step) % playerCount;
    }

    private void ClearStealChain()
    {
        _steal.Clear();
        if (State is GameState.AwaitingStealResponse or GameState.AwaitingStealCardPick)
            State = GameState.RollPhase;
    }

    private void EnsureState(GameState expected)
    {
        if (State != expected)
            throw new InvalidOperationException($"Invalid state for this action. Expected {expected} but was {State}.");
    }
}