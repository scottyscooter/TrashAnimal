namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>Public lobby projection — never exposes any seat's <c>ClientToken</c>.</summary>
public sealed record LobbyView(
    Guid LobbyId,
    IReadOnlyList<LobbySeatView> Seats,
    bool IsStarted,
    Guid? GameId);

/// <summary>One seat's public info within a <see cref="LobbyView"/>.</summary>
public sealed record LobbySeatView(int SeatIndex, string Nickname);
