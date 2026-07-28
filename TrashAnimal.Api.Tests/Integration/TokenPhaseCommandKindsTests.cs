using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using TrashAnimal.TokenPhase;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Positive-path coverage for the TokenPhase command kinds that <c>TurnTransitionTests</c> doesn't
/// exercise (it only drives the StashTrash "draw" branch): <c>cardPick</c>, <c>doubleStash</c>,
/// <c>recyclePick</c>, and <c>resolveTokenSteal</c> against a live session, driven entirely
/// through the HTTP API rather than the engine directly.
/// </summary>
public sealed class TokenPhaseCommandKindsTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public TokenPhaseCommandKindsTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task StashTrash_PickCard_ViaCardPickCommand_StashesCardFaceDownAndRemovesFromHand()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var stashCard = new Card(CardName.MmmPie);
        p0.AddCards([stashCard]);
        var die = DieMockFactory.CreateSequenced(TokenAction.StashTrash).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.ResolveTokenStashTrash);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.TokenStashTrashStashMode);

        var (status, body) = await _client.CardPickAsync(gameId, 0, stashCard.Id);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);

        var (_, view) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(1, view!.View.OwnStash.FaceDownCount);
        Assert.DoesNotContain(view.View.HandCards, c => c.CardId == stashCard.Id);
    }

    [Fact]
    public async Task DoubleStash_SubmitOneCard_ViaDoubleStashCommand_StashesOnlyThatCard()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var keepCard = new Card(CardName.MmmPie);
        var stashCard = new Card(CardName.Feesh);
        p0.AddCards([keepCard, stashCard]);
        var die = DieMockFactory.CreateSequenced(TokenAction.DoubleStash).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.ResolveTokenDoubleStash);

        var (status, body) = await _client.DoubleStashAsync(gameId, 0, [stashCard.Id]);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);

        var (_, view) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(1, view!.View.OwnStash.FaceDownCount);
        Assert.Contains(view.View.HandCards, c => c.CardId == keepCard.Id);
        Assert.DoesNotContain(view.View.HandCards, c => c.CardId == stashCard.Id);
    }

    [Fact]
    public async Task Bandit_ResponderStashesMatchingCard_ViaCardPickCommand_StashesFaceUp()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var matchingCard = new Card(CardName.MmmPie);
        p1.AddCards([matchingCard]);
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit).Object;
        // Every deck draw is MmmPie, so Bandit's auto-reveal deterministically matches p1's hand card.
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50, CardName.MmmPie).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.ResolveTokenBandit);

        var (_, revealedView) = await _client.GetViewAsync(gameId, playerSeat: 1);
        Assert.Equal(CardName.MmmPie, revealedView!.View.TokenPhase!.BanditRevealedCardName);
        Assert.Equal(1, revealedView.View.TokenPhase.BanditCurrentResponderIndex);

        var (status, body) = await _client.CardPickAsync(gameId, 1, matchingCard.Id);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);

        var (_, view) = await _client.GetViewAsync(gameId, playerSeat: 1);
        Assert.Contains(view!.View.OwnStash.FaceUpCards, c => c.CardId == matchingCard.Id);
    }

    [Fact]
    public async Task Recycle_PickReplacement_ViaRecyclePickCommand_AddsTokenToRemainingTokens()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = DieMockFactory.CreateSequenced(TokenAction.Recycle).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.ResolveTokenRecycle);

        // RecycleChoosingReplacement offers no allowedActions at all — assert that here too, since
        // a UI relying on allowedActions instead of RecycleReplacementOptions would render nothing.
        var (_, beforePick) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Empty(beforePick!.AllowedActions);
        Assert.Contains(TokenAction.Bandit, beforePick.View.TokenPhase!.RecycleReplacementOptions);

        var (status, body) = await _client.RecyclePickAsync(gameId, 0, TokenAction.Bandit);
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);

        var (_, view) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(TokenPhaseStep.ChoosingNextToken, view!.View.TokenPhase!.Step);
        Assert.Contains(TokenAction.Bandit, view.View.TokenPhase.RemainingTokens);
        Assert.Contains(GameAction.ResolveTokenBandit, view.AllowedActions);
    }

    [Fact]
    public async Task ResolveTokenSteal_FullFlow_ThiefReceivesCardFromVictimHand()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var victimCard = new Card(CardName.MmmPie);
        p1.AddCards([victimCard]);
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);

        var (stealStatus, stealBody) = await _client.ResolveTokenStealAsync(gameId, 0, victimSeat: 1);
        Assert.Equal(HttpStatusCode.OK, stealStatus);
        Assert.True(stealBody!.Succeeded, stealBody.ErrorMessage);
        Assert.Equal(GameState.AwaitingStealResponse, stealBody.View!.State);

        await AssertActionSucceedsAsync(gameId, 1, GameAction.StealPass);

        var (_, thiefView) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal(GameState.AwaitingStealCardPick, thiefView!.View.State);
        var pickSlot = Assert.Single(thiefView.View.StealPhase!.ThiefPickSlots!);
        Assert.Equal(victimCard.Id, pickSlot.CardId);

        var (pickStatus, pickBody) = await _client.CardPickAsync(gameId, 0, victimCard.Id);
        Assert.Equal(HttpStatusCode.OK, pickStatus);
        Assert.True(pickBody!.Succeeded, pickBody.ErrorMessage);

        var (_, finalThiefView) = await _client.GetViewAsync(gameId, playerSeat: 0);
        Assert.Contains(finalThiefView!.View.HandCards, c => c.CardId == victimCard.Id);
        Assert.Equal(GameState.TokenPhase, finalThiefView.View.State);
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
