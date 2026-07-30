namespace TrashAnimal.Api.Application;

public sealed record GameCommandResult(
    bool Success,
    string? ErrorMessage,
    GameView? View,
    IReadOnlyList<GameAction>? AllowedActions,
    string? InfoMessage = null)
{
    public static GameCommandResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null);

    public static GameCommandResult Ok(GameView view, IReadOnlyList<GameAction> allowedActions, string? infoMessage = null) =>
        new(true, null, view, allowedActions, infoMessage);
}
