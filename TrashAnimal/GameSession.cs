using TrashAnimal.GameLog;
using TrashAnimal.RollPhase;
using TrashAnimal.TokenPhase;

namespace TrashAnimal;

public sealed partial class GameSession
{
    private readonly IDrawPile _drawPile;
    private readonly RollPhaseGameplayHandlerRegistry _rollPhaseHandlers =
        new(RollPhaseGameplayHandlers.CreateDefault());
    private readonly List<Player> _players;
    private readonly YumYumWindow _yumYumWindow = new();
    private readonly TokenPhaseCoordinator _tokenPhaseCoordinator;
    private readonly IGameLogRecorder _logRecorder = new GameLogRecorder();
    private bool _canRoll;
    private bool _hasStoppedRolling;

    private readonly StealAttempt _steal = new();
    private GameState _stealResumeState = GameState.RollPhase;
    private Guid? _bustRecoveryCardDiscardedId;

    public GameSession(IReadOnlyList<Player> players, IDrawPile drawPile)
    {
        if (players.Count is < 2 or > 4)
            throw new ArgumentException("Player count must be between 2 and 4.", nameof(players));

        _drawPile = drawPile ?? throw new ArgumentNullException(nameof(drawPile));
        _players = new List<Player>(players);
        _tokenPhaseCoordinator = new TokenPhaseCoordinator(this);
        CurrentPlayerIndex = 0;
        BeginTurn();
    }

    public IReadOnlyList<Player> Players => _players;
    public int CurrentPlayerIndex { get; private set; }
    public Player CurrentPlayer => _players[CurrentPlayerIndex];
    public PhaseOneState PhaseOne { get; } = new();
    public List<Card> DiscardPile { get; } = new();
    public IDrawPile DrawPile => _drawPile;

    public GameState State { get; private set; } = GameState.RollPhase;
    public IReadOnlyList<TokenAction> LastPhaseTwoTokens { get; private set; } = Array.Empty<TokenAction>();

    public bool IsPhaseOneActive { get; private set; }

    public bool AwaitingYumYumWindow => _yumYumWindow.IsAwaiting;

    public Func<int, IReadOnlyList<Card>, Card?>? OnFeeshCardSelection { get; set; }

    public Func<int, IReadOnlyList<int>, int>? ChooseShinyStealVictim { get; set; }

    public int? StealThiefIndex => _steal.ThiefIndex;
    public int? StealVictimIndex => _steal.VictimIndex;
    public StealTargetZone? InitialStealTargetZone => _steal.InitialStealTargetZone;

    internal GameState StealResumeState => _stealResumeState;

    internal void ArmStealResumeState(GameState state) => _stealResumeState = state;

    internal void ResetStealResumeStateToRollPhase() => _stealResumeState = GameState.RollPhase;

    internal void SetGameState(GameState state) => State = state;

    internal StealAttempt Steal => _steal;

    internal void CompleteTokenPhaseAndEndTurn()
    {
        _tokenPhaseCoordinator.Clear();
        State = GameState.TurnEnd;
        RecordLogEvent(new TurnResolvedEvent(0, TurnNumber, CurrentPlayerIndex));
    }

    public void BeginTurn()
    {
        TurnNumber++;
        ResetStealResumeStateToRollPhase();
        ClearStealChain();
        CurrentPlayer.Hand.ClearNewlyAddedFlags();
        IsPhaseOneActive = true;
        _yumYumWindow.Reset();
        PhaseOne.Reset();
        _canRoll = true;
        _hasStoppedRolling = false;
        _bustRecoveryCardDiscardedId = null;
        LastPhaseTwoTokens = Array.Empty<TokenAction>();
        _tokenPhaseCoordinator.Clear();
        State = GameState.RollPhase;
    }

    public IReadOnlyList<GameAction> GetAllowedActionsForPlayer(int playerIndex)
    {
        if (State == GameState.GameEnded)
            return Array.Empty<GameAction>();

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
                var hasYum = _players[playerIndex].Hand.Any(e => e.Card.Name == CardName.Yumyum);
                return hasYum
                    ? new[] { GameAction.YumYumPlay, GameAction.YumYumPass }
                    : new[] { GameAction.YumYumPass };
            }

            return Array.Empty<GameAction>();
        }

        if (State == GameState.TokenPhase && _tokenPhaseCoordinator.IsActive)
            return _tokenPhaseCoordinator.GetAllowedActions(playerIndex);

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

    /// <param name="resolvedWithNoEffectToken">Set to the token that fizzled (produced no effect) if this
    /// action caused a token to complete/repeat and fizzle in the process (e.g. a Bandit response that
    /// closes the response window and, via MmmPie, repeats into an empty deck); null otherwise.</param>
    public bool ApplyAction(int playerIndex, GameAction action, Die die, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;

        if (State == GameState.GameEnded)
        {
            error = "The game has ended.";
            return false;
        }

        var allowed = GetAllowedActionsForPlayer(playerIndex);
        if (!allowed.Contains(action))
        {
            error = "Action is not allowed right now.";
            return false;
        }

        switch (action)
        {
            case GameAction.RollDie:
                var rollResult = RollDie(die);
                RecordLogEvent(new DieRolledEvent(0, TurnNumber, playerIndex, rollResult.Rolled, rollResult.Status == RollStatus.Busted));
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
                return TryBustAndEndTurn(out error);

            case GameAction.YumYumPlay:
                return TryYumYumRespond(playerIndex, playYumYum: true, out error);

            case GameAction.YumYumPass:
                return TryYumYumRespond(playerIndex, playYumYum: false, out error);

            case GameAction.StealPass:
                return TryStealPass(playerIndex, out error);

            case GameAction.StealPlayDoggo:
                return TryStealPlayDoggo(playerIndex, out error, out resolvedWithNoEffectToken);

            case GameAction.StealPlayKitteh:
                return TryStealPlayKitteh(playerIndex, out error);

            case GameAction.EndTurn:
                EndTurn();
                return true;

            case GameAction.TokenBanditMatchPass:
                return TryBanditPass(playerIndex, out error, out resolvedWithNoEffectToken);

            default:
                if (State == GameState.TokenPhase && _tokenPhaseCoordinator.IsActive)
                    return _tokenPhaseCoordinator.TryApplyGameAction(playerIndex, action, out error, out resolvedWithNoEffectToken);

                error = "Unknown action.";
                return false;
        }
    }

    public bool TryAdvanceToResolveTokens(out string? error)
    {
        error = null;
        EnsureState(GameState.RollPhase);

        if (PhaseOne.IsBusted)
        {
            error = "Cannot advance to resolve tokens while busted; play a recovery card or choose AbandonBust.";
            return false;
        }

        GoToPhaseTwo(CurrentPlayerIndex, PhaseOne.Tokens);
        return true;
    }

    public bool TryBustAndEndTurn(out string? error)
    {
        error = null;
        EnsureState(GameState.RollPhase);

        if (!PhaseOne.IsBusted)
        {
            error = "AbandonBust is only allowed after a bust.";
            return false;
        }

        RecordLogEvent(new PlayerBustedEvent(0, TurnNumber, CurrentPlayerIndex));

        var drawn = _drawPile.DealCards(1).ToList();
        CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);
        RegisterDrawOutcome(drawn);

        GoToPhaseTwo(CurrentPlayerIndex, Array.Empty<TokenAction>());
        EndTurn();
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

    public void EndTurn()
    {
        if (State != GameState.TurnEnd)
            throw new InvalidOperationException("Turn cannot end until TokenPhase has completed.");

        RecordLogEvent(new TurnEndedEvent(0, TurnNumber, CurrentPlayerIndex));

        if (_endGamePendingAfterCurrentTurn)
        {
            FinalizeGameEnd();
            return;
        }

        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        BeginTurn();
    }

    public int? GetCurrentYumYumResponderIndex() => _yumYumWindow.GetCurrentResponderPlayerIndex();

    public IEnumerable<int> EnumerateOpponentIndicesClockwise()
    {
        for (var step = 1; step < _players.Count; step++)
            yield return (CurrentPlayerIndex + step) % _players.Count;
    }

    public static IEnumerable<int> GetOpponentIndicesClockwise(int currentPlayerIndex, int playerCount)
    {
        for (var step = 1; step < playerCount; step++)
            yield return (currentPlayerIndex + step) % playerCount;
    }

    private void GoToPhaseTwo(int playerIndex, IReadOnlyList<TokenAction> tokens)
    {
        IsPhaseOneActive = false;
        _yumYumWindow.Reset();
        ClearStealChain();
        _hasStoppedRolling = true;
        LastPhaseTwoTokens = tokens.ToList();

        if (tokens.Count == 0)
        {
            _tokenPhaseCoordinator.Clear();
            State = GameState.TurnEnd;
            RecordLogEvent(new TurnResolvedEvent(0, TurnNumber, playerIndex));
            return;
        }

        _tokenPhaseCoordinator.Begin(tokens);
        State = GameState.TokenPhase;
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
