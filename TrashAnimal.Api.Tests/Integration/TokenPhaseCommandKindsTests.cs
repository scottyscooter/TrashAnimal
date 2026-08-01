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
        Assert.Single(view!.View.OwnStash.FaceDownCards);
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
        Assert.Single(view!.View.OwnStash.FaceDownCards);
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

        // Regression assertions for the Steal-exhaustion bug: Steal was the only token collected this
        // turn, so completing it must remove it from RemainingTokens and end the turn — previously the
        // API's explicit-victim-choice path never touched RemainingTokens/ActiveToken at all, so the
        // token was offered forever and the turn could never complete.
        Assert.Equal(GameState.TurnEnd, finalThiefView.View.State);
        Assert.DoesNotContain(GameAction.ResolveTokenSteal, finalThiefView.AllowedActions);
        Assert.Contains(GameAction.EndTurn, finalThiefView.AllowedActions);
    }

    [Fact]
    public async Task ResolveTokenSteal_WithNullVictimSeat_AutoResolvesWhenNoOpponentHasCards()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        // p1's hand is intentionally left empty — the fizzle condition under test.
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);

        var (status, body) = await _client.ResolveTokenStealAsync(gameId, 0, victimSeat: null);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(body.InfoMessage));
        Assert.Equal(GameState.TurnEnd, body.View!.State);
        Assert.DoesNotContain(GameAction.ResolveTokenSteal, body.AllowedActions!);
    }

    [Fact]
    public async Task MmmPie_ThenCompletingFirstSteal_CascadesIntoFizzledRepeat_PopulatesInfoMessage()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var mmmPie = new Card(CardName.MmmPie);
        p0.AddCards([mmmPie]);
        var victimCard = new Card(CardName.Feesh);
        p1.AddCards([victimCard]); // exactly one card — the first steal succeeds, then p1's hand is empty
        var die = DieMockFactory.CreateSequenced(TokenAction.Steal).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(50).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.PlayMmmPieTokenPhase);

        var (startStatus, startBody) = await _client.ResolveTokenStealAsync(gameId, 0, victimSeat: 1);
        Assert.Equal(HttpStatusCode.OK, startStatus);
        Assert.True(startBody!.Succeeded, startBody.ErrorMessage);
        Assert.Null(startBody.InfoMessage);

        await AssertActionSucceedsAsync(gameId, 1, GameAction.StealPass);

        var (_, thiefView) = await _client.GetViewAsync(gameId, playerSeat: 0);
        var pickSlot = Assert.Single(thiefView!.View.StealPhase!.ThiefPickSlots!);

        // Completing this pick finishes the first Steal resolution; MmmPie's repeat cascades Steal
        // immediately (via TokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase), finds zero remaining
        // candidates, and fizzles. This must populate InfoMessage on the CardPickCommand response even
        // though CardPickCommand is a different command than the one that started the Steal token — the
        // gap this test guards against.
        var (pickStatus, pickBody) = await _client.CardPickAsync(gameId, 0, pickSlot.CardId);
        Assert.Equal(HttpStatusCode.OK, pickStatus);
        Assert.True(pickBody!.Succeeded, pickBody.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(pickBody.InfoMessage));
        Assert.Contains("Steal", pickBody.InfoMessage);
        Assert.Equal(GameState.TurnEnd, pickBody.View!.State);
    }

    [Fact]
    public async Task Bandit_EmptyDeck_ViaPlainPlayActionCommand_PopulatesInfoMessage()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var die = DieMockFactory.CreateSequenced(TokenAction.Bandit).Object;
        var session = new GameSession([p0, p1], DrawPileMockFactory.CreateWithCards(0).Object);
        _factory.SessionRepository.RegisterSession(gameId, session, die);

        await AssertActionSucceedsAsync(gameId, 0, GameAction.RollDie);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.StopRolling);
        await AssertActionSucceedsAsync(gameId, 1, GameAction.YumYumPass);
        await AssertActionSucceedsAsync(gameId, 0, GameAction.AdvanceToResolveTokens);

        // A first-pick Bandit-with-empty-deck fizzle triggered via a plain PlayActionCommand (not the
        // dedicated Steal API) — the case that was silently broken even without MmmPie in the picture,
        // since only ExecuteTokenStealUnlockedAsync's bespoke bool ever reported a fizzle before this fix.
        var (status, body) = await _client.SubmitCommandAsync(gameId, new PlayActionCommand(0, GameAction.ResolveTokenBandit));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(body!.Succeeded, body.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(body.InfoMessage));
        Assert.Contains("Bandit", body.InfoMessage);
        Assert.Equal(GameState.TurnEnd, body.View!.State);
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
