using TrashAnimal;

namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>
/// Response body for POST /games/{gameId}/commands.
/// On success, <see cref="View"/> and <see cref="AllowedActions"/> are populated. <see cref="InfoMessage"/> is
/// an optional, non-error informational note for a successful command that didn't do what a naive caller might
/// expect (e.g. a Steal token that auto-resolved with no effect because no opponent had a card to steal).
/// On failure, <see cref="ErrorMessage"/> describes the rejection reason.
/// </summary>
public sealed record GameCommandResponse(
    bool Succeeded,
    string? ErrorMessage,
    GameView? View,
    IReadOnlyList<GameAction>? AllowedActions,
    string? InfoMessage = null)
{
    public static GameCommandResponse FromSuccess(GameView view, IReadOnlyList<GameAction> allowedActions, string? infoMessage = null) =>
        new(true, null, view, allowedActions, infoMessage);

    public static GameCommandResponse FromFailure(string errorMessage) =>
        new(false, errorMessage, null, null);
}
