namespace TrashAnimal;

public sealed class GameSession
{
    private readonly IPhaseTwo _phaseTwo;
    private readonly List<Player> _players;
    private readonly List<int> _yumYumResponders = new();
    private int _yumYumResponderPos;

    public GameSession(IReadOnlyList<Player> players, IPhaseTwo phaseTwo)
    {
        if (players.Count is < 2 or > 4)
            throw new ArgumentException("Player count must be between 2 and 4.", nameof(players));

        _phaseTwo = phaseTwo ?? throw new ArgumentNullException(nameof(phaseTwo));
        _players = new List<Player>(players);
        CurrentPlayerIndex = 0;
        BeginTurn();
    }

    public IReadOnlyList<Player> Players => _players;
    public int CurrentPlayerIndex { get; private set; }
    public Player CurrentPlayer => _players[CurrentPlayerIndex];
    public PhaseOneState PhaseOne { get; } = new();
    public List<Card> DiscardPile { get; } = new();

    public GameState State { get; private set; } = GameState.Phase1Rolling;
    public IReadOnlyList<TokenAction> LastPhaseTwoTokens { get; private set; } = Array.Empty<TokenAction>();

    /// <summary>True while the active player may take Phase 1 actions (including after a bust until resolved or abandoned).</summary>
    public bool IsPhaseOneActive { get; private set; }

    /// <summary>After a voluntary stop, before Phase 2: opponents respond in clockwise order; at most one Yum Yum per stop.</summary>
    public bool AwaitingYumYumWindow { get; private set; }

    /// <summary>Optional hook when Shiny or Feesh is played (effects stubbed until Phase 2).</summary>
    public Action<int, CardName>? OnShinyFeeshPlayed { get; set; }

    public void BeginTurn()
    {
        IsPhaseOneActive = true;
        AwaitingYumYumWindow = false;
        PhaseOne.Reset();
        _yumYumResponders.Clear();
        _yumYumResponderPos = 0;
        LastPhaseTwoTokens = Array.Empty<TokenAction>();
        State = GameState.Phase1Rolling;
    }


    public PhaseOneRollResult RollDie(Die die)
    {
        EnsureState(GameState.Phase1Rolling);
        if (AwaitingYumYumWindow)
            throw new InvalidOperationException("Resolve the Yum Yum window before rolling.");
        return PhaseOne.TryRollForToken(die);
    }

    /// <summary>Active player chooses to stop before Phase 2. Opens the Yum Yum window when other players exist.</summary>
    public bool TryRequestVoluntaryStop(out string? error)
    {
        error = null;
        EnsureState(GameState.Phase1Rolling);
        if (!IsPhaseOneActive)
        {
            error = "Phase 1 is not active.";
            return false;
        }

        if (AwaitingYumYumWindow)
        {
            error = "Already awaiting Yum Yum responses.";
            return false;
        }

        if (!PhaseOne.CanVoluntarilyStop())
        {
            error = "Cannot stop while busted or forced rolls remain.";
            return false;
        }

        // todo: Not needed because player count for a game should be 2 to 4 players
        /* if (_players.Count <= 1)
        {
            GoToPhaseTwo(CurrentPlayerIndex, PhaseOne.Tokens);
            return true;
        } */

        _yumYumResponders.Clear();
        _yumYumResponders.AddRange(GetOpponentIndicesClockwise(CurrentPlayerIndex, _players.Count));
        _yumYumResponderPos = 0;
        AwaitingYumYumWindow = true;
        State = GameState.AwaitingYumYum;
        return true;
    }

    public int? GetCurrentYumYumResponderIndex()
    {
        if (!AwaitingYumYumWindow) return null;
        if (_yumYumResponderPos >= _yumYumResponders.Count) return null;
        return _yumYumResponders[_yumYumResponderPos];
    }

    /// <summary>Opponent at the current response slot passes or plays Yum Yum (at most one play per stop).</summary>
    public bool TryYumYumRespond(int opponentPlayerIndex, bool playYumYum, out string? error)
    {
        EnsureState(GameState.AwaitingYumYum);
        // todo: This whole interaction needs to go out to all other players at the same time and then whoever responds first is the one who forces the active player to roll again
        // should be async with about a 10 second response window for each player before continuing the game
        error = null;
        if (!AwaitingYumYumWindow)
        {
            error = "Yum Yum window is not open.";
            return false;
        }

        if (_yumYumResponderPos >= _yumYumResponders.Count)
        {
            error = "Yum Yum window already finished.";
            return false;
        }

        var expected = _yumYumResponders[_yumYumResponderPos];
        if (opponentPlayerIndex != expected)
        {
            error = $"It is not this opponent's turn to respond (expected player index {expected}).";
            return false;
        }

        var opponent = _players[opponentPlayerIndex];
        if (playYumYum)
        {
            if (!opponent.TryRemoveCard(CardName.Yumyum, out var yum))
            {
                error = "Opponent does not have a Yum Yum card.";
                return false;
            }

            DiscardPile.Add(yum);
            PhaseOne.AddForcedRoll();
            CloseYumYumWindowAfterInterrupt();
            State = GameState.Phase1Rolling;
            return true;
        }

        _yumYumResponderPos++;
        if (_yumYumResponderPos >= _yumYumResponders.Count)
            CompleteYumYumWindowAllPassed();

        return true;
    }

    public bool TryRecoverFromBustWithNanners(out string? error)
    {
        error = null;
        EnsureState(GameState.Phase1Rolling);
        if (!EnsureActivePhaseOneForCurrentPlayer(out error)) return false;
        if (!PhaseOne.IsBusted) // todo: Nanners shouldn't even be a chooseable option for the player unless the player is already in a busted state
        {
            error = "Not busted.";
            return false;
        }

        if (!CurrentPlayer.TryRemoveCard(CardName.Nanners, out var card))
        {
            error = "No Nanners card in hand.";
            return false;
        }

        DiscardPile.Add(card);
        PhaseOne.ClearBustIgnoringLastRoll();
        CloseYumYumWindowAfterInterrupt();
        GoToPhaseTwo(CurrentPlayerIndex, PhaseOne.Tokens);
        return true;
    }

    public bool TryRecoverFromBustWithBlammo(out string? error)
    {
        error = null;
        EnsureState(GameState.Phase1Rolling);
        if (!EnsureActivePhaseOneForCurrentPlayer(out error)) return false;
        if (!PhaseOne.IsBusted) // todo: Blammo shouldn't even be a chooseable option for the player unless the player is already in a busted state
        {
            error = "Not busted.";
            return false;
        }

        if (!CurrentPlayer.TryRemoveCard(CardName.Blammo, out var card))
        {
            error = "No Blammo card in hand.";
            return false;
        }

        DiscardPile.Add(card);
        PhaseOne.ClearBustIgnoringLastRoll();
        return true;
    }

    public bool TryRecoverFromBustWithBlammoThenStop(out string? error)
    {
        if (!TryRecoverFromBustWithBlammo(out error))
            return false;
        return TryRequestVoluntaryStop(out error);
    }

    /// <summary>Default: bust with no Nanners/Blammo — Phase 2 receives no tokens from this Phase 1.</summary>
    public bool TryAbandonPhaseOneUnrecoveredBust(out string? error)
    {
        error = null;
        EnsureState(GameState.Phase1Rolling);
        if (!EnsureActivePhaseOneForCurrentPlayer(out error)) return false; // todo: shouldn't need this when the game engine is driving the workflow as it would already know if phase one is active and only call these methods when it is
        if (!PhaseOne.IsBusted) // todo: This should not be an option but rather automatically selected if the player does not respond with a way to clear the busted state
        {
            error = "Not busted.";
            return false;
        }

        GoToPhaseTwo(CurrentPlayerIndex, Array.Empty<TokenAction>());
        return true;
    }

    public bool TryPlayShinyOrFeesh(int playerIndex, CardName cardName, out string? error)
    {
        error = null;
        EnsureState(GameState.Phase1Rolling);
        if (cardName is not (CardName.Shiny or CardName.Feesh))
        {
            error = "Only Shiny or Feesh may be played with this action.";
            return false;
        }

        if (!IsPhaseOneActive)
        {
            error = "Shiny/Feesh may only be played during Phase 1 of the active player's turn.";
            return false;
        }

        if (playerIndex != CurrentPlayerIndex)
        {
            error = "Only the active player may play Shiny or Feesh.";
            return false;
        }

        if (!_players[playerIndex].TryRemoveCard(cardName, out var card))
        {
            error = $"No {cardName} in hand.";
            return false;
        }

        DiscardPile.Add(card);
        OnShinyFeeshPlayed?.Invoke(playerIndex, cardName);
        return true;
    }

    public IReadOnlyList<GameAction> GetAllowedActionsForPlayer(int playerIndex)
    {
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

        if (State != GameState.Phase1Rolling)
            return Array.Empty<GameAction>();

        var actions = new List<GameAction>();

        // Present in order: roll, optional stop, playable cards, then abandon bust (when busted).
        if (!PhaseOne.IsBusted)
        {
            actions.Add(GameAction.RollDie);
            if (PhaseOne.CanVoluntarilyStop())
                actions.Add(GameAction.StopRolling);
        }

        if (CurrentPlayer.Hand.Any(c => c.Name == CardName.Shiny)) actions.Add(GameAction.PlayShiny);
        if (CurrentPlayer.Hand.Any(c => c.Name == CardName.Feesh)) actions.Add(GameAction.PlayFeesh);

        if (PhaseOne.IsBusted)
        {
            if (CurrentPlayer.Hand.Any(c => c.Name == CardName.Nanners)) actions.Add(GameAction.PlayNanners);
            if (CurrentPlayer.Hand.Any(c => c.Name == CardName.Blammo)) actions.Add(GameAction.PlayBlammo);
            actions.Add(GameAction.AbandonBust);
        }

        return actions;
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

            case GameAction.PlayShiny:
                return TryPlayShinyOrFeesh(playerIndex, CardName.Shiny, out error);

            case GameAction.PlayFeesh:
                return TryPlayShinyOrFeesh(playerIndex, CardName.Feesh, out error);

            case GameAction.PlayNanners:
                return TryRecoverFromBustWithNanners(out error);

            case GameAction.PlayBlammo:
                return TryRecoverFromBustWithBlammo(out error);

            case GameAction.AbandonBust:
                return TryAbandonPhaseOneUnrecoveredBust(out error);

            case GameAction.YumYumPlay:
                return TryYumYumRespond(playerIndex, playYumYum: true, out error);

            case GameAction.YumYumPass:
                return TryYumYumRespond(playerIndex, playYumYum: false, out error);

            case GameAction.EndTurn:
                EndTurn();
                return true;

            default:
                error = "Unknown action.";
                return false;
        }
    }

    public GameView GetViewForPlayer(int playerIndex)
    {
        var responderIndex = GetCurrentYumYumResponderIndex();
        var responderName = responderIndex is null ? null : _players[responderIndex.Value].Name;

        var hand = _players[playerIndex].Hand.Select(c => c.Name).ToList();

        return new GameView(
            State,
            CurrentPlayerIndex,
            CurrentPlayer.Name,
            PhaseOne.IsBusted,
            PhaseOne.ForcedRollRemaining,
            PhaseOne.Tokens,
            hand,
            responderIndex,
            responderName
        );
    }

    public void EndTurn()
    {
        if (State != GameState.TurnEnd)
            throw new InvalidOperationException("Turn cannot end until Phase 2 has completed.");

        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        BeginTurn();
    }

    /// <summary>Advance to the next player and start their Phase 1.</summary>
    [Obsolete("Use EndTurn() once State == TurnEnd.")]
    public void AdvanceToNextPlayer()
    {
        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
        BeginTurn();
    }

    private bool EnsureActivePhaseOneForCurrentPlayer(out string? error)
    {
        if (!IsPhaseOneActive)
        {
            error = "Phase 1 is not active.";
            return false;
        }
        
        error = null;
        return true;
    }

    private void CloseYumYumWindowAfterInterrupt()
    {
        AwaitingYumYumWindow = false;
        _yumYumResponders.Clear();
        _yumYumResponderPos = 0;
    }

    private void CompleteYumYumWindowAllPassed()
    {
        AwaitingYumYumWindow = false;
        _yumYumResponders.Clear();
        _yumYumResponderPos = 0;
        GoToPhaseTwo(CurrentPlayerIndex, PhaseOne.Tokens);
    }

    private void GoToPhaseTwo(int playerIndex, IReadOnlyList<TokenAction> tokens)
    {
        IsPhaseOneActive = false;
        AwaitingYumYumWindow = false;
        State = GameState.Phase2;
        _phaseTwo.ResolvePhaseTwo(playerIndex, tokens);
        LastPhaseTwoTokens = tokens.ToList();
        State = GameState.TurnEnd;
    }

    public static IEnumerable<int> GetOpponentIndicesClockwise(int currentPlayerIndex, int playerCount)
    {
        for (var step = 1; step < playerCount; step++)
            yield return (currentPlayerIndex + step) % playerCount;
    }

    private void EnsureState(GameState expected)
    {
        if (State != expected)
            throw new InvalidOperationException($"Invalid state for this action. Expected {expected} but was {State}.");
    }
}
