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

    [Fact]
    public async Task PlayShinyCommand_DuringTokenPhase_BeginsStashSteal()
    {
        var (gameId, session, p0, p1) = ArrangeSessionInTokenPhase();

        p0.AddCards([new Card(CardName.Shiny)]);
        p1.AddToStash(new Card(CardName.Feesh), faceUp: false);

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayShinyCommand(PlayerSeat: 0, VictimSeat: 1));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded);
        Assert.Equal(GameState.AwaitingStealResponse, session.State);
        Assert.DoesNotContain(session.Players[0].Hand, e => e.Card.Name == CardName.Shiny);
        Assert.Contains(session.DiscardPile, c => c.Name == CardName.Shiny);
    }

    [Fact]
    public async Task PlayFeeshCommand_DuringTokenPhase_MovesCardFromDiscardToHand()
    {
        var (gameId, session, p0, _) = ArrangeSessionInTokenPhase();

        p0.AddCards([new Card(CardName.Feesh)]);
        var targetCard = new Card(CardName.Kitteh);
        session.DiscardPile.Add(targetCard);

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new PlayFeeshCommand(PlayerSeat: 0, CardId: targetCard.Id));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded);
        Assert.Equal(GameState.TokenPhase, session.State);
        Assert.Contains(session.Players[0].Hand, e => e.Card.Id == targetCard.Id);
        Assert.DoesNotContain(session.DiscardPile, c => c.Id == targetCard.Id);
        Assert.Contains(session.DiscardPile, c => c.Name == CardName.Feesh);
    }

    /// <summary>
    /// Drives a fresh two-player session to <see cref="GameState.TokenPhase"/> and registers it,
    /// returning the players so a test can seed specific hand/stash/discard content before acting.
    /// </summary>
    private (Guid GameId, GameSession Session, Player P0, Player P1) ArrangeSessionInTokenPhase()
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
        return (gameId, session, p0, p1);
    }
}
