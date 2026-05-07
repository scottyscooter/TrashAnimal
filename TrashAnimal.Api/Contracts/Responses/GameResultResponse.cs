using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>
/// Response body for GET /games/{gameId}/result.
/// Available only when the session has reached <see cref="GameState.GameEnded"/>.
/// </summary>
public sealed record GameResultResponse(
    IReadOnlyList<GameEndScoreLine> ScoreLines,
    int WinningPlayerIndex);
