using System.Net;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

public sealed class GameCreationTests : IClassFixture<TrashApiTestFactory>
{
    private readonly GameApiClient _client;

    public GameCreationTests(TrashApiTestFactory factory)
    {
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task CreateGame_WithTwoPlayers_Returns201Created()
    {
        var (status, body) = await _client.CreateGameAsync(["Alice", "Bob"]);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(body);
    }

    [Fact]
    public async Task CreateGame_WithFourPlayers_Returns201Created()
    {
        var (status, body) = await _client.CreateGameAsync(["Alice", "Bob", "Carol", "Dave"]);

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(body);
    }

    [Fact]
    public async Task CreateGame_ResponseContainsValidGameId()
    {
        var (_, body) = await _client.CreateGameAsync(["Alice", "Bob"]);

        Assert.NotEqual(Guid.Empty, body!.GameId);
    }

    [Fact]
    public async Task CreateGame_ResponseContainsViewWithRollPhaseState()
    {
        var (_, body) = await _client.CreateGameAsync(["Alice", "Bob"]);

        Assert.NotNull(body!.View);
        Assert.Equal(GameState.RollPhase, body.View.State);
        Assert.Equal(0, body.View.CurrentPlayerIndex);
        Assert.Equal("Alice", body.View.CurrentPlayerName);
    }

    [Fact]
    public async Task CreateGame_ResponseContainsAllowedActionsForSeatZero()
    {
        var (_, body) = await _client.CreateGameAsync(["Alice", "Bob"]);

        Assert.NotEmpty(body!.AllowedActions);
        Assert.Contains(GameAction.RollDie, body.AllowedActions);
    }

    [Fact]
    public async Task CreateGame_WithOnePlayer_Returns400BadRequest()
    {
        var (status, _) = await _client.CreateGameAsync(["Alice"]);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }

    [Fact]
    public async Task CreateGame_WithFivePlayers_Returns400BadRequest()
    {
        var (status, _) = await _client.CreateGameAsync(["Alice", "Bob", "Carol", "Dave", "Eve"]);

        Assert.Equal(HttpStatusCode.BadRequest, status);
    }
}
