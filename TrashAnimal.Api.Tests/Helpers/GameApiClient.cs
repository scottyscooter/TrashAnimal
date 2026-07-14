using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// Typed HTTP client for the TrashAnimal API. Wraps the four REST endpoints with strongly-typed
/// request/response handling and shared <see cref="JsonSerializerOptions"/> that mirror the API's
/// enum-as-string policy.
/// </summary>
public sealed class GameApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;

    public GameApiClient(HttpClient http) => _http = http;

    public async Task<(HttpStatusCode Status, GameCreationResponse? Body)> CreateGameAsync(
        IReadOnlyList<string> playerNames, int? dieSeed = null)
    {
        var response = await _http.PostAsJsonAsync("/games", new CreateGameRequest(playerNames, dieSeed), JsonOptions);
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, null);

        var body = await response.Content.ReadFromJsonAsync<GameCreationResponse>(JsonOptions);
        return (response.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, PlayerViewResponse? Body)> GetViewAsync(
        Guid gameId, int playerSeat)
    {
        var response = await _http.GetAsync($"/games/{gameId}/view?playerSeat={playerSeat}");
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, null);

        var body = await response.Content.ReadFromJsonAsync<PlayerViewResponse>(JsonOptions);
        return (response.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, GameCommandResponse? Body)> SubmitCommandAsync(
        Guid gameId, SubmitCommandRequest request)
    {
        var response = await _http.PostAsJsonAsync($"/games/{gameId}/commands", request, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<GameCommandResponse>(JsonOptions);
        return (response.StatusCode, body);
    }

    public async Task<(HttpStatusCode Status, GameResultResponse? Body)> GetResultAsync(Guid gameId)
    {
        var response = await _http.GetAsync($"/games/{gameId}/result");
        if (!response.IsSuccessStatusCode)
            return (response.StatusCode, null);

        var body = await response.Content.ReadFromJsonAsync<GameResultResponse>(JsonOptions);
        return (response.StatusCode, body);
    }

    public Task<(HttpStatusCode Status, GameCommandResponse? Body)> RollDieAsync(Guid gameId, int playerSeat) =>
        SubmitCommandAsync(gameId, new SubmitCommandRequest(playerSeat, GameAction.RollDie));

    public Task<(HttpStatusCode Status, GameCommandResponse? Body)> EndTurnAsync(Guid gameId, int playerSeat) =>
        SubmitCommandAsync(gameId, new SubmitCommandRequest(playerSeat, GameAction.EndTurn));
}
