namespace TrashAnimal.Api.Application;

public sealed record GameCreationResult(
    Guid GameId,
    GameView View,
    IReadOnlyList<GameAction> AllowedActions);
