using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>
/// Response body for GET /games/{gameId}/view.
/// Includes the per-player game view, the set of actions the caller may submit right now,
/// and the session revision used to detect missed SignalR notifications on reconnect.
/// </summary>
public sealed record PlayerViewResponse(
    GameView View,
    IReadOnlyList<GameAction> AllowedActions,
    int Revision);
