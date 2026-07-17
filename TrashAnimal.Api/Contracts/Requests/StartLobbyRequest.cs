namespace TrashAnimal.Api.Contracts.Requests;

/// <summary>Request body for POST /lobbies/{lobbyId}/start. <see cref="ClientToken"/> must match seat 0's token.</summary>
public sealed record StartLobbyRequest(string ClientToken);
