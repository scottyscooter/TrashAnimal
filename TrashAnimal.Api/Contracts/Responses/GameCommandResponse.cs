using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>
/// Response body for POST /games/{gameId}/commands.
/// On success, <see cref="View"/> and <see cref="AllowedActions"/> are populated.
/// On failure, <see cref="ErrorMessage"/> describes the rejection reason.
/// </summary>
public sealed record GameCommandResponse(
    bool Succeeded,
    string? ErrorMessage,
    GameView? View,
    IReadOnlyList<GameAction>? AllowedActions)
{
    public static GameCommandResponse FromSuccess(GameView view, IReadOnlyList<GameAction> allowedActions) =>
        new(true, null, view, allowedActions);

    public static GameCommandResponse FromFailure(string errorMessage) =>
        new(false, errorMessage, null, null);
}
