using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using TrashAnimal.TokenPhase;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// HTTP-level regression coverage for the MmmPie-repeated Steal token strand: completing the first
/// steal's card pick must surface <c>TokenPhaseStep.StealChoosingVictim</c> and offer
/// <c>ResolveTokenSteal</c> instead of leaving the session stuck at <c>ChoosingNextToken</c> with an
/// empty <c>RemainingTokens</c>. See <c>.claude/docs/plans/steal-token-mmmpie-repeat-fix.md</c>.
/// </summary>
public sealed class TokenPhaseStealRepeatTests : IClassFixture<TrashApiTestFactory>
{
    private readonly TrashApiTestFactory _factory;
    private readonly GameApiClient _client;

    public TokenPhaseStealRepeatTests(TrashApiTestFactory factory)
    {
        _factory = factory;
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task MmmPieRepeatOfSteal_OverHttp_SurfacesStealChoosingVictimAndReopensSteal()
    {
        var gameId = Guid.NewGuid();
        var p0 = new Player(0, "Alice");
        var p1 = new Player(1, "Bob");
        var mmmPie = new Card(CardName.MmmPie);
        p0.AddCards([mmmPie]);
        var firstVictimCard = new Card(CardName.Feesh);
        var secondVictimCard = new Card(CardName.Yumyum);
        p1.AddCards([firstVictimCard, secondVictimCard]); // first steal leaves one card behind for the repeat
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

        await AssertActionSucceedsAsync(gameId, 1, GameAction.StealPass);

        var (_, thiefView) = await _client.GetViewAsync(gameId, playerSeat: 0);
        var pickSlot = thiefView!.View.StealPhase!.ThiefPickSlots!.First(); // 2 slots — p1 still has a candidate for the repeat afterward

        // Completing this pick finishes the first Steal resolution and cascades into MmmPie's repeat. With
        // a candidate still remaining (p1's second card), the repeat must park in StealChoosingVictim and
        // offer ResolveTokenSteal — not fail silently with a swallowed error, and not leave the session
        // stuck at ChoosingNextToken with an empty RemainingTokens (the originally reported bug).
        var (pickStatus, pickBody) = await _client.CardPickAsync(gameId, 0, pickSlot.CardId);
        Assert.Equal(HttpStatusCode.OK, pickStatus);
        Assert.True(pickBody!.Succeeded, pickBody.ErrorMessage);
        Assert.Equal(TokenPhaseStep.StealChoosingVictim, pickBody.View!.TokenPhase!.Step);
        Assert.Equal(TokenAction.Steal, pickBody.View.TokenPhase.ActiveToken);
        Assert.Contains(GameAction.ResolveTokenSteal, pickBody.AllowedActions!);
        Assert.NotEqual(GameState.TurnEnd, pickBody.View.State);

        // Submitting a second ResolveTokenStealCommand against the same victim reopens the steal attempt,
        // reaching the scenario the original bug report could never get to (the victim being independently
        // offered a Doggo block on the repeat).
        var (reopenStatus, reopenBody) = await _client.ResolveTokenStealAsync(gameId, 0, victimSeat: 1);
        Assert.Equal(HttpStatusCode.OK, reopenStatus);
        Assert.True(reopenBody!.Succeeded, reopenBody.ErrorMessage);
        Assert.Equal(GameState.AwaitingStealResponse, reopenBody.View!.State);
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
