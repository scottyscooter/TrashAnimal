using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Drives full turns through the HTTP API (<c>POST /games/{id}/commands</c>) and asserts
/// <c>GET /games/{id}/view?playerSeat=X</c> shows correctly-redacted, differently-worded log lines
/// per seat for the same underlying events, and that the log accumulates across multiple commands.
/// Uses <see cref="TestableGameSessionRepository"/> + <see cref="SequencedDie"/>/<see cref="CountingDrawPile"/>
/// (no repository mocking, per project convention).
/// </summary>
public sealed class GameLogIntegrationTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public GameLogIntegrationTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task StashSteal_ViaShiny_ShowsDifferentlyWordedLogLines_ToThiefAndVictim()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var stashedCard = new Card(CardName.MmmPie);
        p1.AddToStash(stashedCard, faceUp: false);
        p0.Hand.Clear();
        p0.Hand.Add(new Card(CardName.Shiny));
        var die = new SequencedDie(TokenAction.StashTrash);
        var session = new GameSession([p0, p1], new CountingDrawPile(50));
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        var (shinyStatus, shinyBody) = await _client.SubmitCommandAsync(
            gameId, new PlayShinyCommand(0, VictimSeat: 1));
        Assert.Equal(HttpStatusCode.OK, shinyStatus);
        Assert.True(shinyBody!.Succeeded, shinyBody.ErrorMessage);

        await AssertActionSucceedsAsync(gameId, 1, GameAction.StealPass);

        var (pickStatus, pickBody) = await _client.CardPickAsync(gameId, 0, stashedCard.Id);
        Assert.Equal(HttpStatusCode.OK, pickStatus);
        Assert.True(pickBody!.Succeeded, pickBody.ErrorMessage);

        var (_, thiefView) = await _client.GetViewAsync(gameId, playerSeat: 0);
        var (_, victimView) = await _client.GetViewAsync(gameId, playerSeat: 1);

        // Same sequence numbers for both viewers...
        var thiefSeqs = thiefView!.View.Log.Select(e => e.SequenceNumber).ToList();
        var victimSeqs = victimView!.View.Log.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(thiefSeqs, victimSeqs);

        // ...but the steal-completion line's wording differs: the thief sees the card's identity,
        // the victim does not.
        var thiefStealLine = thiefView.View.Log.Single(e => e.Message.Contains("stole"));
        var victimStealLine = victimView.View.Log.Single(e => e.Message.Contains("stole"));

        Assert.Contains("MmmPie", thiefStealLine.Message);
        Assert.DoesNotContain("MmmPie", victimStealLine.Message);
        Assert.NotEqual(thiefStealLine.Message, victimStealLine.Message);
    }

    [Fact]
    public async Task Log_Accumulates_AcrossMultipleCommands_WithinAGame()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = new SequencedDie(TokenAction.StashTrash);
        var session = new GameSession([p0, p1], new CountingDrawPile(50));
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        var (_, afterCreate) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Empty(afterCreate!.View.Log);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        var (_, afterRoll) = await _client.GetViewAsync(gameId, playerSeat: 0);
        var countAfterRoll = afterRoll!.View.Log.Count;

        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        var (_, afterStop) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.True(afterStop!.View.Log.Count > countAfterRoll, "Expected StopRolling to add a log entry.");

        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.ResolveTokenStashTrash);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.TokenStashTrashDrawOne);

        var (_, afterTokenPhase) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.True(
            afterTokenPhase!.View.Log.Count > afterStop.View.Log.Count,
            "Expected TokenPhase resolution to add further log entries.");

        // Earlier entries are preserved (accumulation, not replacement) and sequence numbers stay ordered.
        var sequenceNumbers = afterTokenPhase.View.Log.Select(e => e.SequenceNumber).ToList();
        Assert.Equal(sequenceNumbers.OrderBy(n => n), sequenceNumbers);
        Assert.Equal(sequenceNumbers.Distinct().Count(), sequenceNumbers.Count);
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
