namespace TrashAnimal;

public sealed partial class GameSession
{
    private bool _endGamePendingAfterCurrentTurn;
    private readonly ScoreManager _scoreManager = new();
    private GameEndResult? _cachedGameEndResult;

    internal void RegisterDrawOutcome(IReadOnlyList<Card> dealt)
    {
        if (dealt.Count > 0 && _drawPile.GetDeckCount() == 0)
            _endGamePendingAfterCurrentTurn = true;
    }

    /// <summary>Returns the final scoreboard after the game has ended.</summary>
    public IReadOnlyList<GameEndScoreLine> GetGameEndScoreSummary()
    {
        if (State != GameState.GameEnded)
            throw new InvalidOperationException("Score summary is only available after the game has ended.");
        if (_cachedGameEndResult is null)
            throw new InvalidOperationException("Game end result has not been calculated.");

        return _cachedGameEndResult.ScoreLines;
    }

    /// <summary>
    /// Returns the complete end-of-game scoring result, including the winning player index.
    /// Call only after <see cref="GameState.GameEnded"/>.
    /// </summary>
    public GameEndResult GetGameEndResult()
    {
        if (State != GameState.GameEnded)
            throw new InvalidOperationException("Game result is only available after the game has ended.");
        if (_cachedGameEndResult is null)
            throw new InvalidOperationException("Game end result has not been calculated.");

        return _cachedGameEndResult;
    }

    private void FinalizeGameEnd()
    {
        foreach (var player in _players)
        {
            var cardsFromHand = player.Hand.Select(entry => entry.Card).ToList();
            player.Hand.Clear();
            DiscardPile.AddRange(cardsFromHand);
        }

        _cachedGameEndResult = _scoreManager.ComputeResult(_players);
        _tokenPhaseCoordinator.Clear();
        _steal.Clear();
        _endGamePendingAfterCurrentTurn = false;
        State = GameState.GameEnded;
    }
}
