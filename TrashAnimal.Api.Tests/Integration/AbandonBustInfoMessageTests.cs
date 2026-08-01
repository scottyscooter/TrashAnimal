using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using TrashAnimal.TokenPhase;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// HTTP-level coverage for issue #15's consolation-card message (B1). See
/// <c>.claude/docs/plans/debug-notes-b-card-selection-interaction-model.md</c>.
/// </summary>
public sealed class AbandonBustInfoMessageTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public AbandonBustInfoMessageTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task AbandonBust_OverHttp_SurfacesConsolationDrawInfoMessage()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit, TokenAction.Bandit).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(0, GameAction.AbandonBust));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);
        Assert.Equal("Drew 1 card (bust consolation) — turn ended.", body.InfoMessage);
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
