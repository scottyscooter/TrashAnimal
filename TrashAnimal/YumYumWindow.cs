namespace TrashAnimal;

/// <summary>
/// Clockwise opponent response window after a voluntary stop. Playing <see cref="CardName.Yumyum"/> discards it and
/// forces the active player to roll again via <see cref="PhaseOneState.AddForcedRoll"/>; passing advances the slot
/// until everyone has passed or the window is interrupted by a play.
/// </summary>
public sealed class YumYumWindow
{
    private readonly List<int> _clockwiseResponderIndices = new();
    private int _currentResponderSlot;
    private bool _isAwaiting;

    public bool IsAwaiting => _isAwaiting;

    public void Reset()
    {
        _isAwaiting = false;
        _clockwiseResponderIndices.Clear();
        _currentResponderSlot = 0;
    }

    /// <summary>Starts the window: opponents (in order) may pass or play Yum Yum.</summary>
    public void Open(IEnumerable<int> clockwiseOpponentPlayerIndices)
    {
        _clockwiseResponderIndices.Clear();
        _clockwiseResponderIndices.AddRange(clockwiseOpponentPlayerIndices);
        _currentResponderSlot = 0;
        _isAwaiting = true;
    }

    public int? GetCurrentResponderPlayerIndex()
    {
        if (!_isAwaiting)
            return null;
        if (_currentResponderSlot >= _clockwiseResponderIndices.Count)
            return null;
        return _clockwiseResponderIndices[_currentResponderSlot];
    }

    /// <summary>
    /// Opponent at the current slot passes or plays Yum Yum (at most one play per stop).
    /// todo: This whole interaction should go out to all other players at the same time; whoever responds first forces the active player to roll again;
    /// async with ~10s response window per player before continuing.
    /// </summary>
    public bool TryRespond(
        int opponentPlayerIndex,
        bool playYumYum,
        IList<Player> players,
        IList<Card> discardPile,
        PhaseOneState phaseOne,
        Action onYumYumPlayedAllowRollsAgain,
        Action onWindowClosedReturnToRollPhase,
        out string? error)
    {
        error = null;
        if (!_isAwaiting)
        {
            error = "Yum Yum window is not open.";
            return false;
        }

        if (_currentResponderSlot >= _clockwiseResponderIndices.Count)
        {
            error = "Yum Yum window already finished.";
            return false;
        }

        var expectedPlayerIndex = _clockwiseResponderIndices[_currentResponderSlot];
        if (opponentPlayerIndex != expectedPlayerIndex)
        {
            error = $"It is not this opponent's turn to respond (expected player index {expectedPlayerIndex}).";
            return false;
        }

        var opponent = players[opponentPlayerIndex];
        if (playYumYum)
        {
            if (!opponent.TryRemoveCard(CardName.Yumyum, out var yum))
            {
                error = "Opponent does not have a Yum Yum card.";
                return false;
            }

            discardPile.Add(yum);
            phaseOne.AddForcedRoll();
            onYumYumPlayedAllowRollsAgain();
            CloseReturningToRollPhase(onWindowClosedReturnToRollPhase);
            return true;
        }

        _currentResponderSlot++;
        if (_currentResponderSlot >= _clockwiseResponderIndices.Count)
            CloseReturningToRollPhase(onWindowClosedReturnToRollPhase);

        return true;
    }

    private void CloseReturningToRollPhase(Action onWindowClosedReturnToRollPhase)
    {
        Reset();
        onWindowClosedReturnToRollPhase();
    }
}
