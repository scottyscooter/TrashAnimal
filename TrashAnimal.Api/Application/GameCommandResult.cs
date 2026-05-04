namespace TrashAnimal.Api.Application;

public sealed record GameCommandResult(
    bool Success,
    string? ErrorMessage,
    GameView? View,
    IReadOnlyList<GameAction>? AllowedActions)
{
    public static GameCommandResult Failure(string errorMessage) =>
        new(false, errorMessage, null, null);

    public static GameCommandResult Ok(GameView view, IReadOnlyList<GameAction> allowedActions) =>
        new(true, null, view, allowedActions);
}
