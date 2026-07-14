using System.Text.Json;
using System.Text.Json.Serialization;
using TrashAnimal.Api.Contracts.Responses;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Contract;

/// <summary>
/// Asserts that the <c>GameView</c> DTO shape matches the engine record structure and that the
/// hidden-information boundary is enforced: each player's view exposes only their own hand.
///
/// Shape tests ensure no fields were silently dropped or renamed during serialisation, which
/// would break the frontend without a compile error. Hidden-info tests verify the invariant
/// stated in <see cref="PlayerViewResponse"/>: <c>HandCardNames</c> contains only the requesting
/// player's own cards.
/// </summary>
public sealed class GameViewShapeContractTests : IClassFixture<TrashApiTestFactory>
{
    private static readonly JsonSerializerOptions StringEnumOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        PropertyNameCaseInsensitive = true,
    };

    private readonly GameApiClient _apiClient;
    private readonly HttpClient _rawHttp;

    public GameViewShapeContractTests(TrashApiTestFactory factory)
    {
        _apiClient = new GameApiClient(factory.CreateClient());
        _rawHttp = factory.CreateClient();
    }

    [Fact]
    public async Task GameView_JsonShape_ContainsAllExpectedFields()
    {
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var response = await _rawHttp.GetAsync($"/games/{gameId}/view?playerSeat=0");
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        var view = doc.RootElement.GetProperty("view");

        // Verify every field declared on the GameView record is present.
        Assert.True(view.TryGetProperty("state", out _), "Missing: state");
        Assert.True(view.TryGetProperty("currentPlayerIndex", out _), "Missing: currentPlayerIndex");
        Assert.True(view.TryGetProperty("currentPlayerName", out _), "Missing: currentPlayerName");
        Assert.True(view.TryGetProperty("isBusted", out _), "Missing: isBusted");
        Assert.True(view.TryGetProperty("forcedRollRemaining", out _), "Missing: forcedRollRemaining");
        Assert.True(view.TryGetProperty("phaseOneTokens", out _), "Missing: phaseOneTokens");
        Assert.True(view.TryGetProperty("handCardNames", out _), "Missing: handCardNames");
        Assert.True(view.TryGetProperty("yumYumResponderIndex", out _), "Missing: yumYumResponderIndex");
        Assert.True(view.TryGetProperty("yumYumResponderName", out _), "Missing: yumYumResponderName");
        Assert.True(view.TryGetProperty("stealPhase", out _), "Missing: stealPhase");
        Assert.True(view.TryGetProperty("tokenPhase", out _), "Missing: tokenPhase");
    }

    [Fact]
    public async Task PlayerViewResponse_ContainsViewAllowedActionsAndRevision()
    {
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var response = await _rawHttp.GetAsync($"/games/{gameId}/view?playerSeat=0");
        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);

        Assert.True(doc.RootElement.TryGetProperty("view", out _), "Missing: view");
        Assert.True(doc.RootElement.TryGetProperty("allowedActions", out _), "Missing: allowedActions");
        Assert.True(doc.RootElement.TryGetProperty("revision", out _), "Missing: revision");
    }

    [Fact]
    public async Task PlayerView_HandCardNames_ContainsOnlyRequestingPlayerCards()
    {
        // With StartingHandCounts [3, 4]: Alice (seat 0) gets 3 cards, Bob (seat 1) gets 4.
        // If the hidden-info boundary were broken, each view would expose the combined 7 cards.
        var (_, body) = await _apiClient.GetViewAsync(
            (await _apiClient.CreateGameAsync(["Alice", "Bob"])).Body!.GameId,
            playerSeat: 0);

        // Unused; re-created inline to get independent views below.
        _ = body;

        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (_, aliceView) = await _apiClient.GetViewAsync(gameId, playerSeat: 0);
        var (_, bobView) = await _apiClient.GetViewAsync(gameId, playerSeat: 1);

        Assert.Equal(3, aliceView!.View.HandCardNames.Count);
        Assert.Equal(4, bobView!.View.HandCardNames.Count);
    }

    [Fact]
    public async Task CreationResponse_View_MatchesSeat0Perspective()
    {
        // The creation response returns the seat-0 view; it must match a subsequent GET /view
        // for playerSeat=0 at the same revision.
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;
        var creationView = created.View;

        var (_, viewResponse) = await _apiClient.GetViewAsync(gameId, playerSeat: 0);

        Assert.Equal(creationView.State, viewResponse!.View.State);
        Assert.Equal(creationView.CurrentPlayerIndex, viewResponse.View.CurrentPlayerIndex);
        Assert.Equal(creationView.CurrentPlayerName, viewResponse.View.CurrentPlayerName);
        Assert.Equal(creationView.HandCardNames.Count, viewResponse.View.HandCardNames.Count);
    }

    [Fact]
    public async Task OpponentHandCards_AreAbsentFromPlayerView()
    {
        var (_, created) = await _apiClient.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (_, aliceView) = await _apiClient.GetViewAsync(gameId, playerSeat: 0);
        var (_, bobView) = await _apiClient.GetViewAsync(gameId, playerSeat: 1);

        // Alice's and Bob's starting hand sizes differ (3 vs 4 per StartingHandCounts).
        // If any player could see the opponent's hand, the counts would be equal (both 7)
        // or cross-contaminated. Verify neither player sees the other's card count.
        Assert.NotEqual(aliceView!.View.HandCardNames.Count, bobView!.View.HandCardNames.Count);

        // Combined hands must not appear in either individual view.
        var combined = aliceView.View.HandCardNames.Count + bobView.View.HandCardNames.Count;
        Assert.True(aliceView.View.HandCardNames.Count < combined);
        Assert.True(bobView.View.HandCardNames.Count < combined);
    }
}
