using System.Net;
using System.Text.Json;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Contract;

/// <summary>
/// Asserts that <c>GameView.log</c> round-trips through JSON with camelCase property names matching
/// the frontend's frozen <c>GameLogEntryView</c> contract: <c>sequenceNumber</c>, <c>turnNumber</c>,
/// <c>actingPlayerSeat</c>, <c>message</c>.
/// </summary>
public sealed class GameLogContractTests : IClassFixture<TrashApiTestFactory>
{
    private readonly GameApiClient _apiClient;
    private readonly HttpClient _rawHttp;

    public GameLogContractTests(TrashApiTestFactory factory)
    {
        _apiClient = new GameApiClient(factory.CreateClient());
        _rawHttp = factory.CreateClient();
    }

    [Fact]
    public async Task GameView_JsonShape_ContainsLogProperty()
    {
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var response = await _rawHttp.GetAsync($"/games/{gameId}/view?playerSeat=0");
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var view = doc.RootElement.GetProperty("view");
        Assert.True(view.TryGetProperty("log", out var logProperty), "Missing: log");
        Assert.Equal(JsonValueKind.Array, logProperty.ValueKind);
    }

    [Fact]
    public async Task GameView_Log_AccumulatesEntries_WithCamelCasePropertyNames()
    {
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // Roll then voluntarily stop: emits a TurnStoppedRollingEvent, giving the log at least one entry.
        var (rollStatus, rollBody) = await _apiClient.RollDieAsync(gameId, playerSeat: 0);
        Assert.Equal(HttpStatusCode.OK, rollStatus);
        Assert.True(rollBody!.Succeeded, rollBody.ErrorMessage);

        var (stopStatus, stopBody) = await _apiClient.SubmitCommandAsync(
            gameId, new PlayActionCommand(0, GameAction.StopRolling));
        Assert.Equal(HttpStatusCode.OK, stopStatus);
        Assert.True(stopBody!.Succeeded, stopBody.ErrorMessage);

        var response = await _rawHttp.GetAsync($"/games/{gameId}/view?playerSeat=0");
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var logEntries = doc.RootElement.GetProperty("view").GetProperty("log");
        Assert.True(logEntries.GetArrayLength() >= 1, "Expected at least one log entry after StopRolling.");

        var firstEntry = logEntries[0];
        Assert.True(firstEntry.TryGetProperty("sequenceNumber", out _), "Missing: sequenceNumber");
        Assert.True(firstEntry.TryGetProperty("turnNumber", out _), "Missing: turnNumber");
        Assert.True(firstEntry.TryGetProperty("actingPlayerSeat", out _), "Missing: actingPlayerSeat");
        Assert.True(firstEntry.TryGetProperty("message", out var messageProperty), "Missing: message");
        Assert.Equal(JsonValueKind.String, messageProperty.ValueKind);
    }

    [Fact]
    public async Task GameView_Log_TypedDeserialization_PopulatesGameLogEntryView()
    {
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        await _apiClient.RollDieAsync(gameId, playerSeat: 0);
        await _apiClient.SubmitCommandAsync(gameId, new PlayActionCommand(0, GameAction.StopRolling));

        var (_, view) = await _apiClient.GetViewAsync(gameId, playerSeat: 0);

        Assert.NotEmpty(view!.View.Log);
        var entry = view.View.Log[0];
        Assert.True(entry.SequenceNumber > 0);
        Assert.Equal(1, entry.TurnNumber);
        Assert.Equal(0, entry.ActingPlayerSeat);
        Assert.False(string.IsNullOrWhiteSpace(entry.Message));
    }
}
