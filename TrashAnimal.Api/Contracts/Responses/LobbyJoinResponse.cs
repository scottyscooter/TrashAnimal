namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>Response body for POST /lobbies and POST /lobbies/{lobbyId}/players.</summary>
public sealed record LobbyJoinResponse(LobbyView Lobby, int SeatIndex, string ClientToken);
