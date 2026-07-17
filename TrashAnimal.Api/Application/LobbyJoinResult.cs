using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Application;

public sealed record LobbyJoinResult(LobbyView Lobby, int SeatIndex, string ClientToken);

public enum LobbyJoinFailureReason
{
    LobbyNotFound,
    LobbyAlreadyStarted,
    LobbyFull,
    DuplicateNickname,
}

public sealed record LobbyJoinOutcome(bool Success, LobbyJoinFailureReason? FailureReason, LobbyJoinResult? Result)
{
    public static LobbyJoinOutcome Ok(LobbyJoinResult result) => new(true, null, result);
    public static LobbyJoinOutcome Fail(LobbyJoinFailureReason reason) => new(false, reason, null);
}
