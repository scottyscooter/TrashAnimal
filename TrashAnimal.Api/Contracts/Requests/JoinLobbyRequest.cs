namespace TrashAnimal.Api.Contracts.Requests;

/// <summary>Request body for POST /lobbies/{lobbyId}/players.</summary>
public sealed record JoinLobbyRequest(string Nickname);
