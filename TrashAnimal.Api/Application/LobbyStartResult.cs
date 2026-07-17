namespace TrashAnimal.Api.Application;

public sealed record LobbyStartResult(Guid GameId);

public enum LobbyStartFailureReason
{
    LobbyNotFound,
    NotAdmin,
    InvalidSeatCount,
    AlreadyStarted,
}

public sealed record LobbyStartOutcome(bool Success, LobbyStartFailureReason? FailureReason, LobbyStartResult? Result)
{
    public static LobbyStartOutcome Ok(LobbyStartResult result) => new(true, null, result);
    public static LobbyStartOutcome Fail(LobbyStartFailureReason reason) => new(false, reason, null);
}
