using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Verifies that token-phase interrupt card plays are accepted at the right moment and rejected
/// otherwise, testing the phase-gating boundary through the HTTP layer.
/// </summary>
public sealed class TokenPhaseInterruptTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public TokenPhaseInterruptTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task PlayFeeshTokenPhase_DuringRollPhase_Returns422()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(0, GameAction.PlayFeeshTokenPhase));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.NotNull(body.ErrorMessage);
    }

    [Fact]
    public async Task PlayShinyTokenPhase_DuringRollPhase_Returns422()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(0, GameAction.PlayShinyTokenPhase));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.NotNull(body.ErrorMessage);
    }

    [Fact]
    public async Task RollDie_DuringTokenPhase_Returns422()
    {
        // Arrange: inject a session and drive it to TokenPhase via engine APIs directly.
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = new SequencedDie(TokenAction.StashTrash);
        var session = new GameSession([p0, p1], new CountingDrawPile(50));

        session.ApplyAction(0, GameAction.RollDie, die, out _);
        session.ApplyAction(0, GameAction.StopRolling, die, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        // Act: try to roll the die while in TokenPhase.
        var (status, body) = await _client.RollDieAsync(gameId, playerSeat: 0);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.NotNull(body.ErrorMessage);
    }

    [Fact]
    public async Task StopRolling_DuringTokenPhase_Returns422()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = new SequencedDie(TokenAction.StashTrash);
        var session = new GameSession([p0, p1], new CountingDrawPile(50));

        session.ApplyAction(0, GameAction.RollDie, die, out _);
        session.ApplyAction(0, GameAction.StopRolling, die, out _);
        session.ApplyAction(1, GameAction.YumYumPass, die, out _);
        session.ApplyAction(0, GameAction.AdvanceToResolveTokens, die, out _);

        Assert.Equal(GameState.TokenPhase, session.State);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(0, GameAction.StopRolling));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
    }
}
