using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Verifies that <c>GET /games/{gameId}/result</c> returns 404 while a game is active and 200
/// with correct score lines once the game reaches <see cref="GameState.GameEnded"/>.
///
/// Uses a single-card draw pile so that <see cref="GameAction.AbandonBust"/> exhausts the deck
/// and triggers the game-end path, matching the scenario covered by engine tests.
/// </summary>
public sealed class EndGameScoringTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public EndGameScoringTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task GetResult_BeforeGameEnds_Returns404()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.GetResultAsync(gameId);

        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Null(body);
    }

    [Fact]
    public async Task GetResult_AfterGameEnds_Returns200WithScoreLines()
    {
        // Arrange: create session with 1-card draw pile so AbandonBust ends the game.
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");

        // Give Alice a stashed card so she scores a non-zero total.
        p0.AddToStash(new Card(CardName.Blammo), faceUp: true);

        // Two identical Bandit rolls trigger a bust.
        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        var session = new GameSession([p0, p1], new CountingDrawPile(1));
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        // Act: bust and abandon, which draws the last card and ends the game.
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.RollDie);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.RollDie);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.AbandonBust);

        var (status, body) = await _client.GetResultAsync(gameId);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(body);
        Assert.Equal(2, body.ScoreLines.Count);

        var aliceLine = body.ScoreLines.Single(l => l.PlayerName == "Alice");
        var bobLine = body.ScoreLines.Single(l => l.PlayerName == "Bob");
        Assert.Equal(1, aliceLine.TotalScore);
        Assert.Equal(0, bobLine.TotalScore);
        Assert.Equal(0, body.WinningPlayerIndex);
    }

    [Fact]
    public async Task GetResult_AfterGameEnds_ScoreLines_IncludeAllPlayers()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = new SequencedDie(TokenAction.Bandit, TokenAction.Bandit);
        var session = new GameSession([p0, p1], new CountingDrawPile(1));
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.RollDie);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.RollDie);
        await AssertCommandSucceedsAsync(gameId, playerSeat: 0, GameAction.AbandonBust);

        var (_, body) = await _client.GetResultAsync(gameId);

        Assert.Equal(2, body!.ScoreLines.Count);
        Assert.Contains(body.ScoreLines, l => l.PlayerIndex == 0 && l.PlayerName == "Alice");
        Assert.Contains(body.ScoreLines, l => l.PlayerIndex == 1 && l.PlayerName == "Bob");
    }

    private async Task AssertCommandSucceedsAsync(Guid gameId, int playerSeat, GameAction action)
    {
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayActionCommand(playerSeat, action));

        Assert.True(
            status == HttpStatusCode.OK && body?.Succeeded == true,
            $"Command {action} for playerSeat={playerSeat} failed: HTTP {status}, error=\"{body?.ErrorMessage}\"");
    }
}
