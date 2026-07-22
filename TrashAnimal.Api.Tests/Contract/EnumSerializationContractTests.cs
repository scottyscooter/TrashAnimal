using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Contract;

/// <summary>
/// Asserts that all enum fields in API request and response bodies are serialised as strings,
/// never as integers. The frontend relies on named values (e.g. <c>"RollPhase"</c>); receiving
/// an integer instead would silently break rendering without a compile-time error.
///
/// Tests read the raw JSON response body and verify the string form of each enum field,
/// independent of any client-side <see cref="JsonStringEnumConverter"/>.
/// </summary>
public sealed class EnumSerializationContractTests : IClassFixture<TrashApiTestFactory>
{
    private static readonly JsonSerializerOptions StringEnumOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _rawHttp;

    public EnumSerializationContractTests(TrashApiTestFactory factory)
    {
        _rawHttp = factory.CreateClient();
    }

    [Fact]
    public async Task CreateGame_Response_GameView_State_IsString()
    {
        var response = await _rawHttp.PostAsJsonAsync(
            "/games",
            new CreateGameRequest(["Alice", "Bob"]),
            StringEnumOptions);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var stateToken = doc.RootElement.GetProperty("view").GetProperty("state");
        Assert.Equal(JsonValueKind.String, stateToken.ValueKind);
        Assert.Equal("RollPhase", stateToken.GetString());
    }

    [Fact]
    public async Task CreateGame_Response_AllowedActions_AreStrings()
    {
        var response = await _rawHttp.PostAsJsonAsync(
            "/games",
            new CreateGameRequest(["Alice", "Bob"]),
            StringEnumOptions);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var allowedActions = doc.RootElement.GetProperty("allowedActions");
        Assert.Equal(JsonValueKind.Array, allowedActions.ValueKind);
        Assert.NotEmpty(allowedActions.EnumerateArray());

        foreach (var element in allowedActions.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, element.ValueKind);
        }
    }

    [Fact]
    public async Task CreateGame_Response_PhaseOneTokens_AreStrings()
    {
        var response = await _rawHttp.PostAsJsonAsync(
            "/games",
            new CreateGameRequest(["Alice", "Bob"]),
            StringEnumOptions);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var tokens = doc.RootElement.GetProperty("view").GetProperty("phaseOneTokens");
        Assert.Equal(JsonValueKind.Array, tokens.ValueKind);

        foreach (var token in tokens.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, token.ValueKind);
        }
    }

    [Fact]
    public async Task CreateGame_Response_HandCardNames_AreStrings()
    {
        var response = await _rawHttp.PostAsJsonAsync(
            "/games",
            new CreateGameRequest(["Alice", "Bob"]),
            StringEnumOptions);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var handCards = doc.RootElement.GetProperty("view").GetProperty("handCardNames");
        Assert.Equal(JsonValueKind.Array, handCards.ValueKind);

        foreach (var card in handCards.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, card.ValueKind);
        }
    }

    [Fact]
    public async Task SubmitCommand_SuccessResponse_AllowedActions_AreStrings()
    {
        var createResponse = await _rawHttp.PostAsJsonAsync(
            "/games",
            new CreateGameRequest(["Alice", "Bob"]),
            StringEnumOptions);

        var createJson = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createJson);
        var gameId = createDoc.RootElement.GetProperty("gameId").GetGuid();

        var commandResponse = await _rawHttp.PostAsJsonAsync(
            $"/games/{gameId}/commands",
            (GameCommandRequest)new PlayActionCommand(0, GameAction.RollDie),
            StringEnumOptions);

        var rawJson = await commandResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var allowedActions = doc.RootElement.GetProperty("allowedActions");
        Assert.Equal(JsonValueKind.Array, allowedActions.ValueKind);

        foreach (var action in allowedActions.EnumerateArray())
        {
            Assert.Equal(JsonValueKind.String, action.ValueKind);
        }
    }

    [Fact]
    public async Task SubmitCommand_SuccessResponse_View_State_IsString()
    {
        var createResponse = await _rawHttp.PostAsJsonAsync(
            "/games",
            new CreateGameRequest(["Alice", "Bob"]),
            StringEnumOptions);

        var createJson = await createResponse.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createJson);
        var gameId = createDoc.RootElement.GetProperty("gameId").GetGuid();

        var commandResponse = await _rawHttp.PostAsJsonAsync(
            $"/games/{gameId}/commands",
            (GameCommandRequest)new PlayActionCommand(0, GameAction.RollDie),
            StringEnumOptions);

        var rawJson = await commandResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var state = doc.RootElement.GetProperty("view").GetProperty("state");
        Assert.Equal(JsonValueKind.String, state.ValueKind);
        Assert.False(string.IsNullOrEmpty(state.GetString()));
    }
}
