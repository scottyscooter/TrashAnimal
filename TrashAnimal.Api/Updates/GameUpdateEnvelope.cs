namespace TrashAnimal.Api.Updates;

public sealed record GameUpdateEnvelope(
    Guid GameId,
    int Revision,
    int ActingPlayerSeat,
    GameState CurrentGameState);
