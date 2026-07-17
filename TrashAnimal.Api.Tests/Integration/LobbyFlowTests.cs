using System.Net;
using TrashAnimal.Api.Tests.Helpers;
using Xunit;

namespace TrashAnimal.Api.Tests.Integration;

public sealed class LobbyFlowTests : IClassFixture<TrashApiTestFactory>
{
    private readonly LobbyApiClient _lobbies;
    private readonly GameApiClient _games;

    public LobbyFlowTests(TrashApiTestFactory factory)
    {
        _lobbies = new LobbyApiClient(factory.CreateClient());
        _games = new GameApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task CreateLobby_Returns201_WithAdminAtSeatZero()
    {
        var (status, body) = await _lobbies.CreateLobbyAsync("Alice");

        Assert.Equal(HttpStatusCode.Created, status);
        Assert.NotNull(body);
        Assert.Equal(0, body!.SeatIndex);
        Assert.NotEmpty(body.ClientToken);
        Assert.Single(body.Lobby.Seats);
        Assert.Equal("Alice", body.Lobby.Seats[0].Nickname);
        Assert.False(body.Lobby.IsStarted);
        Assert.Null(body.Lobby.GameId);
    }

    [Fact]
    public async Task JoinLobby_SecondPlayer_Returns200_AtSeatOne()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;

        var (status, body) = await _lobbies.JoinLobbyAsync(lobbyId, "Bob");

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(body);
        Assert.Equal(1, body!.SeatIndex);
        Assert.Equal(2, body.Lobby.Seats.Count);
    }

    [Fact]
    public async Task JoinLobby_DuplicateNickname_Returns409()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;

        var (status, _) = await _lobbies.JoinLobbyAsync(lobbyId, "alice"); // case-insensitive match

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task JoinLobby_WhenFull_Returns409()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;
        await _lobbies.JoinLobbyAsync(lobbyId, "Bob");
        await _lobbies.JoinLobbyAsync(lobbyId, "Carol");
        await _lobbies.JoinLobbyAsync(lobbyId, "Dave");

        var (status, _) = await _lobbies.JoinLobbyAsync(lobbyId, "Eve");

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task JoinLobby_NotFound_Returns404()
    {
        var (status, _) = await _lobbies.JoinLobbyAsync(Guid.NewGuid(), "Bob");

        Assert.Equal(HttpStatusCode.NotFound, status);
    }

    [Fact]
    public async Task StartLobby_WithWrongToken_Returns403()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;
        await _lobbies.JoinLobbyAsync(lobbyId, "Bob");

        var (status, _) = await _lobbies.StartLobbyAsync(lobbyId, "not-the-real-token");

        Assert.Equal(HttpStatusCode.Forbidden, status);
    }

    [Fact]
    public async Task StartLobby_WithTooFewSeats_Returns422()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;

        var (status, _) = await _lobbies.StartLobbyAsync(lobbyId, created.ClientToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, status);
    }

    [Fact]
    public async Task StartLobby_WithCorrectToken_Returns200_AndResultingGameIdIsPlayable()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;
        await _lobbies.JoinLobbyAsync(lobbyId, "Bob");

        var (status, body) = await _lobbies.StartLobbyAsync(lobbyId, created.ClientToken);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.GameId);

        var (viewStatus, view) = await _games.GetViewAsync(body.GameId, playerSeat: 0);
        Assert.Equal(HttpStatusCode.OK, viewStatus);
        Assert.NotNull(view);
        Assert.Equal(GameState.RollPhase, view!.View.State);
    }

    [Fact]
    public async Task StartLobby_AfterAlreadyStarted_Returns409()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;
        await _lobbies.JoinLobbyAsync(lobbyId, "Bob");
        await _lobbies.StartLobbyAsync(lobbyId, created.ClientToken);

        var (status, _) = await _lobbies.StartLobbyAsync(lobbyId, created.ClientToken);

        Assert.Equal(HttpStatusCode.Conflict, status);
    }

    [Fact]
    public async Task ConcurrentJoins_AtSeatLimit_ExactlyFillsToFourSeats()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;

        // Admin already occupies seat 0; fire 5 concurrent joins racing the remaining 3 seats.
        var nicknames = new[] { "Bob", "Carol", "Dave", "Eve", "Frank" };
        var tasks = nicknames.Select(name => _lobbies.JoinLobbyAsync(lobbyId, name)).ToList();
        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.Status == HttpStatusCode.OK);
        var conflictCount = results.Count(r => r.Status == HttpStatusCode.Conflict);

        Assert.Equal(3, successCount);
        Assert.Equal(2, conflictCount);

        var (_, finalView) = await _lobbies.GetLobbyAsync(lobbyId);
        Assert.Equal(4, finalView!.Seats.Count);

        // No two successful joins may have landed on the same seat index.
        var seatIndices = finalView.Seats.Select(s => s.SeatIndex).ToList();
        Assert.Equal(seatIndices.Distinct().Count(), seatIndices.Count);
    }

    [Fact]
    public async Task ConcurrentJoinAndStart_NeverProducesInconsistentState()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;
        await _lobbies.JoinLobbyAsync(lobbyId, "Bob");

        var startTask = _lobbies.StartLobbyAsync(lobbyId, created.ClientToken);
        var joinTask = _lobbies.JoinLobbyAsync(lobbyId, "Carol");
        await Task.WhenAll(startTask, joinTask);

        var (startStatus, startBody) = await startTask;
        var (joinStatus, joinBody) = await joinTask;

        // Start must always succeed (2 seats is valid) regardless of the race outcome.
        Assert.Equal(HttpStatusCode.OK, startStatus);
        Assert.NotNull(startBody);

        // The join either completed before the lock-guarded start snapshot the roster (200,
        // and the resulting game must have 3 players) or lost the race and was correctly
        // rejected as already-started (409) — there is no outcome where the join silently
        // vanishes or corrupts the lobby's seat list.
        if (joinStatus == HttpStatusCode.OK)
        {
            Assert.NotNull(joinBody);
            var (thirdSeatStatus, _) = await _games.GetViewAsync(startBody!.GameId, playerSeat: 2);
            Assert.Equal(HttpStatusCode.OK, thirdSeatStatus);
        }
        else
        {
            Assert.Equal(HttpStatusCode.Conflict, joinStatus);
        }
    }

    [Fact]
    public async Task StartLobby_SeatIndexMapsToCorrectGamePlayerSeat()
    {
        var (_, created) = await _lobbies.CreateLobbyAsync("Alice");
        var lobbyId = created!.Lobby.LobbyId;
        await _lobbies.JoinLobbyAsync(lobbyId, "Bob");
        await _lobbies.JoinLobbyAsync(lobbyId, "Carol");

        var (_, startBody) = await _lobbies.StartLobbyAsync(lobbyId, created.ClientToken);
        var gameId = startBody!.GameId;

        // Seat 0 (the admin, "Alice") goes first — CurrentPlayerName on the fresh game view
        // confirms lobby seat order was preserved as game player-seat order, not re-derived
        // from some other ordering.
        var (_, viewForSeatZero) = await _games.GetViewAsync(gameId, playerSeat: 0);
        Assert.Equal("Alice", viewForSeatZero!.View.CurrentPlayerName);

        // All three seats must resolve to valid players...
        var (seat1Status, _) = await _games.GetViewAsync(gameId, playerSeat: 1);
        var (seat2Status, _) = await _games.GetViewAsync(gameId, playerSeat: 2);
        Assert.Equal(HttpStatusCode.OK, seat1Status);
        Assert.Equal(HttpStatusCode.OK, seat2Status);

        // ...and a fourth (never-joined) seat must not, confirming exactly 3 players were created.
        var (seat3Status, _) = await _games.GetViewAsync(gameId, playerSeat: 3);
        Assert.Equal(HttpStatusCode.NotFound, seat3Status);
    }
}
