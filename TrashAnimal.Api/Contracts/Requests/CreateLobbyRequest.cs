namespace TrashAnimal.Api.Contracts.Requests;

/// <summary>Request body for POST /lobbies. The caller becomes the admin, seated at index 0.</summary>
public sealed record CreateLobbyRequest(string Nickname);
