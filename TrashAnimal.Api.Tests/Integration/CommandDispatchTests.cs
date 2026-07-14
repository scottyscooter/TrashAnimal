using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Covers HTTP-level command routing: valid actions return 200, disallowed actions return 422,
/// unknown games return 404, and response bodies are correctly shaped for both success and failure.
/// </summary>
public sealed class CommandDispatchTests : IClassFixture<TrashApiTestFactory>
{
    private readonly GameApiClient _client;

    public CommandDispatchTests(TrashApiTestFactory factory)
    {
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task SubmitCommand_ValidAction_Returns200WithSucceededTrue()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.RollDieAsync(gameId, playerSeat: 0);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded);
        Assert.NotNull(body.View);
        Assert.NotEmpty(body.AllowedActions!);
        Assert.Null(body.ErrorMessage);
    }

    [Fact]
    public async Task SubmitCommand_DisallowedAction_Returns422WithErrorMessage()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // EndTurn is never valid as the very first action; game starts in RollPhase.
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.EndTurn));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.NotNull(body.ErrorMessage);
        Assert.Null(body.View);
        Assert.Null(body.AllowedActions);
    }

    [Fact]
    public async Task SubmitCommand_UnknownGameId_Returns404()
    {
        var unknownGameId = Guid.NewGuid();

        var (status, _) = await _client.SubmitCommandAsync(unknownGameId,
            new SubmitCommandRequest(0, GameAction.RollDie));

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task GetView_UnknownGameId_Returns404()
    {
        var unknownGameId = Guid.NewGuid();

        var (status, body) = await _client.GetViewAsync(unknownGameId, playerSeat: 0);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetResult_UnknownGameId_Returns404()
    {
        var unknownGameId = Guid.NewGuid();

        var (status, body) = await _client.GetResultAsync(unknownGameId);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetResult_OnActiveGame_Returns404()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // Game is in RollPhase; result is only available after GameEnded.
        var (status, body) = await _client.GetResultAsync(gameId);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task SubmitCommand_TokenPhaseOnlyAction_DuringRollPhase_Returns422()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // PlayFeeshTokenPhase is only valid during the token phase, not during roll phase.
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.PlayFeeshTokenPhase));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.NotNull(body.ErrorMessage);
    }

    [Fact]
    public async Task GetView_ValidGame_Returns200WithPlayerViewResponse()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.GetViewAsync(gameId, playerSeat: 0);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(body);
        Assert.NotNull(body.View);
        Assert.NotEmpty(body.AllowedActions);
        Assert.True(body.Revision >= 0);
    }
}
