using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>Response body for POST /games.</summary>
public sealed record GameCreationResponse(
    Guid GameId,
    GameView View,
    IReadOnlyList<GameAction> AllowedActions);
