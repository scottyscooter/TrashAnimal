using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using TrashAnimal.TokenPhase;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// HTTP-level regression coverage for the Recycle-with-zero-options fizzle (B1). See
/// <c>.claude/docs/plans/token-zero-option-deadlocks-fix.md</c>.
/// </summary>
public sealed class TokenPhaseZeroOptionTokenTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public TokenPhaseZeroOptionTokenTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Recycle_ZeroOptions_OverHttp_PopulatesInfoMessageAndAdvances()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = DieMockFactory.CreateSequenced(
            TokenAction.StashTrash, TokenAction.DoubleStash, TokenAction.DoubleTrash,
            TokenAction.Bandit, TokenAction.Steal, TokenAction.Recycle).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(0, GameAction.ResolveTokenRecycle));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);
        Assert.NotNull(body.InfoMessage);
        Assert.DoesNotContain(GameAction.ResolveTokenRecycle, body.AllowedActions!);
        Assert.DoesNotContain(TokenAction.Recycle, body.View!.TokenPhase!.RemainingTokens);
    }

    private async Task AssertActionSucceedsAsync(Guid gameId, int playerSeat, GameAction action)
    {
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(playerSeat, action));

        Assert.True(
            status == HttpStatusCode.OK && body?.Succeeded == true,
            $"Command {action} for playerSeat={playerSeat} failed: HTTP {status}, error=\"{body?.ErrorMessage}\"");
    }
}
