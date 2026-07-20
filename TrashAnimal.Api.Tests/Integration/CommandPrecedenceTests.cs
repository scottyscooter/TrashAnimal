using System.Net;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

/// <summary>
/// Pins the field-precedence order <see cref="TrashAnimal.Api.Application.GameApplicationService"/>'s
/// ExecuteCommandUnlockedAsync uses to route a <see cref="SubmitCommandRequest"/>: RecycleReplacement,
/// then CardIds, then Action == PlayFeesh/PlayShiny/ResolveTokenSteal, then a bare CardId (routed by
/// GameState/TokenPhaseStep), and only then the plain Action fallback. The frontend's
/// `gamesApi.ts` relies on this exact order — its contextual command variants (card picks, double
/// stash, recycle pick) send a placeholder `action` value (`EndTurn`) that the backend must never
/// actually interpret as an EndTurn attempt. If a future change to the dispatch order lets `Action`
/// take precedence over `CardId`/`CardIds`/`RecycleReplacement` for any of these cases, that
/// placeholder would silently misroute — these tests fail loudly instead.
/// </summary>
public sealed class CommandPrecedenceTests : IClassFixture<TrashApiTestFactory>
{
    private readonly GameApiClient _client;

    public CommandPrecedenceTests(TrashApiTestFactory factory)
    {
        _client = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task RecycleReplacement_TakesPrecedenceOver_CardIdAndPlayFeeshAction()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // Action is PlayFeesh and CardId is populated — if RecycleReplacement weren't checked
        // first, this would be routed to the PlayFeesh handler instead.
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.PlayFeesh, CardId: Guid.NewGuid(), RecycleReplacement: TokenAction.Bandit));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.Equal("Not choosing a Recycle replacement.", body.ErrorMessage);
    }

    [Fact]
    public async Task CardIds_TakesPrecedenceOver_PlayShinyActionAndCardId()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // Action is PlayShiny and CardId is also populated — if CardIds weren't checked first,
        // this would be routed to the PlayShiny handler (via Action) or the bare-CardId handler.
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.PlayShiny, CardId: Guid.NewGuid(), CardIds: [Guid.NewGuid()], VictimSeat: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.Equal("Not in DoubleStash resolution.", body.ErrorMessage);
    }

    [Fact]
    public async Task PlayFeeshAction_TakesPrecedenceOver_BareCardIdRouting()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // Action == PlayFeesh with only CardId populated (no RecycleReplacement/CardIds). If Action
        // weren't checked before the bare-CardId fallback, this would instead produce "A card pick
        // is not expected in the current game state." (RollPhase is neither AwaitingStealCardPick
        // nor TokenPhase).
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.PlayFeesh, CardId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.Equal("No cards in discard pile to retrieve with Feesh.", body.ErrorMessage);
    }

    [Fact]
    public async Task PlayShinyAction_TakesPrecedenceOver_BareVictimSeatFallthrough()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.PlayShiny, VictimSeat: 1));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.Equal("No opponent has a card in their stash to steal.", body.ErrorMessage);
    }

    [Fact]
    public async Task BareCardId_IsRoutedByGameState_IgnoringAnUnrelatedAction()
    {
        var (_, created) = await _client.CreateGameAsync(["Alice", "Bob"]);
        var gameId = created!.GameId;

        // This is exactly the shape TrashAnimal.Web's gamesApi.ts sends for its contextual card-pick
        // variants: Action is a placeholder ('EndTurn') the backend must never interpret literally,
        // because CardId is populated and takes precedence. RollPhase is neither
        // AwaitingStealCardPick nor TokenPhase, so this must fail with the bare-CardId routing's
        // own rejection — never with EndTurn's "TurnEnd only" rejection.
        var (status, body) = await _client.SubmitCommandAsync(gameId,
            new SubmitCommandRequest(0, GameAction.EndTurn, CardId: Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
        Assert.False(body!.Succeeded);
        Assert.Equal("A card pick is not expected in the current game state.", body.ErrorMessage);
    }
}
