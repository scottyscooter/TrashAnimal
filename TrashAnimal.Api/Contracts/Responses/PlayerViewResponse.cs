namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>
/// Response body for <c>GET /games/{gameId}/view</c>.
/// </summary>
/// <remarks>
/// <b>Hidden-information guarantee:</b> <see cref="GameView.HandCardNames"/> contains only the
/// cards held by the player identified by <c>playerSeat</c>. Opponent hand contents are never
/// included. This boundary is enforced by the engine's
/// <c>GameSession.GetViewForPlayer(playerSeat)</c> projection, which reads exclusively from
/// <c>_players[playerSeat].Hand</c>. The API layer never accesses <c>Player</c>, <c>Hand</c>,
/// <c>Deck</c>, or <c>StashPile</c> directly; all reads go through that projection.
/// </remarks>
/// <param name="View">Per-player projection of the current game state.</param>
/// <param name="AllowedActions">
/// Actions the player identified by <c>playerSeat</c> may legally submit right now.
/// </param>
/// <param name="Revision">
/// Monotonically increasing session revision. Cache this value; on SignalR reconnect,
/// compare against the latest notification's revision and re-fetch if behind.
/// </param>
public sealed record PlayerViewResponse(
    GameView View,
    IReadOnlyList<GameAction> AllowedActions,
    int Revision);
