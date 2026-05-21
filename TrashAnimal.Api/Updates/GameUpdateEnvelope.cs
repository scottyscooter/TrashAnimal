namespace TrashAnimal.Api.Updates;

/// <summary>
/// Lightweight push notification sent to all clients in a game's SignalR group after each
/// successful command. Clients use this as a trigger to call
/// <c>GET /games/{gameId}/view</c> — the full game state is always fetched via REST.
/// </summary>
/// <param name="GameId">Identifies which game was updated.</param>
/// <param name="Revision">
/// Monotonically increasing counter incremented on every successful mutation.
/// Clients cache this value; on SignalR reconnect, compare against the latest view's
/// revision and re-fetch if behind.
/// </param>
/// <param name="ActingPlayerSeat">Zero-based seat index of the player who submitted the command.</param>
/// <param name="CurrentGameState">The game state after the command was applied.</param>
public sealed record GameUpdateEnvelope(
    Guid GameId,
    int Revision,
    int ActingPlayerSeat,
    GameState CurrentGameState);
