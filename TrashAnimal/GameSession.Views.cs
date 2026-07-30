using TrashAnimal.GameLog;

namespace TrashAnimal;

/// <summary>
/// Per-player view projection (<see cref="GetViewForPlayer"/>). Split out of <c>GameSession.cs</c> (kept
/// over this repo's 400-line split-immediately guidance) since view-building is a self-contained,
/// side-effect-free concern.
/// </summary>
public sealed partial class GameSession
{
    public GameView GetViewForPlayer(int playerIndex)
    {
        var responderIndex = GetCurrentYumYumResponderIndex();
        var responderName = responderIndex is null ? null : _players[responderIndex.Value].Name;

        var hand = _players[playerIndex].Hand
            .Select(e => new HandCardView(e.Card.Id, e.Card.Name))
            .ToList();

        var stealPhase = _steal.BuildPhaseView(State, playerIndex, _players);

        var tokenPhase = _tokenPhaseCoordinator.IsActive
            ? _tokenPhaseCoordinator.BuildView(playerIndex)
            : null;

        var opponents = _players
            .Where(p => p.Index != playerIndex)
            .Select(p => new OpponentSummaryView(
                p.Index,
                p.Name,
                p.Hand.Count,
                p.StashPile.Count(e => !e.IsFaceUp),
                p.StashPile.Where(e => e.IsFaceUp)
                    .Select(e => new StashableHandCard(e.Card.Id, e.Card.Name))
                    .ToList()))
            .ToList();

        var discardPile = DiscardPile
            .Select(c => new DiscardCardView(c.Id, c.Name))
            .ToList();

        var ownStashPile = _players[playerIndex].StashPile;
        var ownStash = new OwnStashView(
            ownStashPile.Count(e => !e.IsFaceUp),
            ownStashPile.Where(e => e.IsFaceUp)
                .Select(e => new StashableHandCard(e.Card.Id, e.Card.Name))
                .ToList());

        var log = GameLogProjector.BuildForViewer(_logRecorder.Events, playerIndex, _players);

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
            stealPhase,
            tokenPhase,
            opponents,
            _drawPile.GetDeckCount(),
            discardPile,
            ownStash,
            log);
    }
}
