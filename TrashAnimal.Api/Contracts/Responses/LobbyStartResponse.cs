namespace TrashAnimal.Api.Contracts.Responses;

/// <summary>Response body for POST /lobbies/{lobbyId}/start.</summary>
public sealed record LobbyStartResponse(Guid GameId);
