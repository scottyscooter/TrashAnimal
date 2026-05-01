namespace TrashAnimal;

public sealed partial class GameSession
{
    private bool _endGamePendingAfterCurrentTurn;

    internal void RegisterDrawOutcome(IReadOnlyList<Card> dealt)
    {
        if (dealt.Count > 0 && _drawPile.GetDeckCount() == 0)
            _endGamePendingAfterCurrentTurn = true;
    }

    /// <summary>Stub scoreboard until scoring rules exist; call only after <see cref="GameState.GameEnded"/>.</summary>
    public IReadOnlyList<GameEndScoreLine> GetGameEndScoreSummary()
    {
        if (State != GameState.GameEnded)
            throw new InvalidOperationException("Score summary is only available after the game has ended.");

        return _players
            .Select(p => new GameEndScoreLine(p.Index, p.Name, 0))
            .ToList();
    }

    private void FinalizeGameEnd()
    {
        foreach (var player in _players)
        {
            var cardsFromHand = player.Hand.Select(entry => entry.Card).ToList();
            player.Hand.Clear();
            DiscardPile.AddRange(cardsFromHand);
        }

        _tokenPhaseCoordinator.Clear();
        _steal.Clear();
        _endGamePendingAfterCurrentTurn = false;
        State = GameState.GameEnded;
    }
}
