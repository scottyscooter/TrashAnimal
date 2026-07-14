using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Verifies that a complete roll-phase → token-phase → end-turn cycle advances the active
/// player correctly through the HTTP API. Uses an injected session with a <see cref="SequencedDie"/>
/// that always rolls <see cref="TokenAction.StashTrash"/> so the token-phase path is deterministic.
/// </summary>
public sealed class TurnTransitionTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public TurnTransitionTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task RollDie_Command_Returns200_WithUpdatedView()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.RollDieAsync(gameId, playerSeat: 0);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded);
        Assert.NotNull(body.View);
        Assert.Equal(GameState.RollPhase, body.View.State);
    }

    [Fact]
    public async Task FullTurnCycle_StashTrash_AdvancesCurrentPlayerToNextSeat()
    {
        // Arrange: inject a session with a sequenced die so roll outcomes are deterministic.
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = new SequencedDie(TokenAction.StashTrash);
        var session = new GameSession([p0, p1], new CountingDrawPile(50));
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        // Roll phase: player 0 rolls once then stops.
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.RollDie);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.StopRolling);

        // YumYum window: player 1 passes.
        await AssertCommandSucceedsAsync(gameId, playerSeat: 1, GameAction.YumYumPass);

        // Token phase: resolve StashTrash by drawing one card.
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.AdvanceToResolveTokens);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.ResolveTokenStashTrash);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.TokenStashTrashDrawOne);

        // End turn: advances current player.
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.EndTurn);

        // Assert: player 1 is now the active player and state has reset to RollPhase.
        var (status, view) = await _client.GetViewAsync(gameId, playerSeat: 1);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(GameState.RollPhase, view!.View.State);
        Assert.Equal(1, view.View.CurrentPlayerIndex);
        Assert.Equal("Bob", view.View.CurrentPlayerName);
    }

    [Fact]
    public async Task SubmitCommand_IncreasesRevision_AfterEachSuccessfulCommand()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (_, before) = await _client.GetViewAsync(gameId, playerSeat: 0);
        var revisionBefore = before!.Revision;

        await _client.RollDieAsync(gameId, playerSeat: 0);

        var (_, after) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(revisionBefore + 1, after!.Revision);
    }

    private async Task AssertCommandSucceedsAsync(Guid gameId, int playerSeat, GameAction action)
    {
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(playerSeat, action));

        Assert.True(
            status == HttpStatusCode.OK && body?.Succeeded == true,
            $"Command {action} for playerSeat={playerSeat} failed: HTTP {status}, error=\"{body?.ErrorMessage}\"");
    }
}
