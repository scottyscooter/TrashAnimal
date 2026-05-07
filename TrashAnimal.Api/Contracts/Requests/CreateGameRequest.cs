namespace TrashAnimal.Api.Contracts.Requests;

/// <summary>Request body for POST /games.</summary>
public sealed record CreateGameRequest(
    IReadOnlyList<string> PlayerNames,
    int? DieSeed = null);
