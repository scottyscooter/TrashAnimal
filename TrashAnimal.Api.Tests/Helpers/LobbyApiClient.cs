using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>Typed HTTP client for the Lobbies API, mirroring <see cref="GameApiClient"/>.</summary>
public sealed class LobbyApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public LobbyApiClient(HttpClient http) => _http = http;

    public async Task<(HttpStatusCode Status, LobbyJoinResponse? Body)> CreateLobbyAsync(string nickname)
    {
        var response = await _http.PostAsJsonAsync("/lobbies", new CreateLobbyRequest(nickname), JsonOptions);
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LobbyJoinResponse>(JsonOptions)
            : null;
        return (response.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, LobbyView? Body)> GetLobbyAsync(Guid lobbyId)
    {
        var response = await _http.GetAsync($"/lobbies/{lobbyId}");
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LobbyView>(JsonOptions)
            : null;
        return (response.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, LobbyJoinResponse? Body)> JoinLobbyAsync(Guid lobbyId, string nickname)
    {
        var response = await _http.PostAsJsonAsync($"/lobbies/{lobbyId}/players", new JoinLobbyRequest(nickname), JsonOptions);
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LobbyJoinResponse>(JsonOptions)
            : null;
        return (response.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, LobbyStartResponse? Body)> StartLobbyAsync(Guid lobbyId, string clientToken)
    {
        var response = await _http.PostAsJsonAsync($"/lobbies/{lobbyId}/start", new StartLobbyRequest(clientToken), JsonOptions);
        var body = response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<LobbyStartResponse>(JsonOptions)
            : null;
        return (response.StatusCode, body);
    }
}
