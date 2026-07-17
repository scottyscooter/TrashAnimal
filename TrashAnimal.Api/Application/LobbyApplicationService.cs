using Microsoft.Extensions.Logging;
using TrashAnimal.Api.Contracts.Responses;
using TrashAnimal.Api.Lobbies;
using TrashAnimal.Api.Updates;

namespace TrashAnimal.Api.Application;

/// <summary>
/// The bridge from HTTP/SignalR to in-memory lobby state, and — on start — into the existing
/// <see cref="GameApplicationService"/>. Mirrors that service's per-entity locking discipline:
/// every join/start acquires <see cref="LobbyEntry.Lock"/> for its entire read-check-mutate
/// sequence, so concurrent joins can't both slip past the seat-limit/nickname-uniqueness checks
/// and a double-submitted start can't create two games for the same lobby.
/// </summary>
public sealed class LobbyApplicationService
{
    public const int MinSeats = 2;
    public const int MaxSeats = LobbyEntry.MaxSeats;

    private readonly ILobbyRepository _lobbyRepository;
    private readonly GameApplicationService _gameApplicationService;
    private readonly ILobbyUpdatePublisher _updatePublisher;
    private readonly ILogger<LobbyApplicationService> _logger;

    public LobbyApplicationService(
        ILobbyRepository lobbyRepository,
        GameApplicationService gameApplicationService,
        ILobbyUpdatePublisher updatePublisher,
        ILogger<LobbyApplicationService> logger)
    {
        _lobbyRepository = lobbyRepository;
        _gameApplicationService = gameApplicationService;
        _updatePublisher = updatePublisher;
        _logger = logger;
    }

    public Task<LobbyJoinResult> CreateLobbyAsync(string adminNickname)
    {
        var lobbyId = Guid.NewGuid();
        var entry = new LobbyEntry(lobbyId);
        var seat = entry.AddSeat(adminNickname);

        _lobbyRepository.Add(lobbyId, entry);

        _logger.LogInformation(
            "Lobby {LobbyId} created by admin '{Nickname}' at seat {SeatIndex}.",
            lobbyId, adminNickname, seat.SeatIndex);

        return Task.FromResult(new LobbyJoinResult(BuildView(entry), seat.SeatIndex, seat.ClientToken));
    }

    public Task<LobbyView?> GetLobbyViewAsync(Guid lobbyId)
    {
        var entry = _lobbyRepository.TryGet(lobbyId);
        return Task.FromResult(entry is null ? null : BuildView(entry));
    }

    public async Task<LobbyJoinOutcome> JoinLobbyAsync(Guid lobbyId, string nickname)
    {
        var entry = _lobbyRepository.TryGet(lobbyId);
        if (entry is null)
        {
            _logger.LogWarning("JoinLobby failed: lobby {LobbyId} not found.", lobbyId);
            return LobbyJoinOutcome.Fail(LobbyJoinFailureReason.LobbyNotFound);
        }

        await entry.Lock.WaitAsync();
        try
        {
            if (entry.IsStarted)
            {
                _logger.LogWarning("JoinLobby failed: lobby {LobbyId} already started.", lobbyId);
                return LobbyJoinOutcome.Fail(LobbyJoinFailureReason.LobbyAlreadyStarted);
            }

            if (entry.Seats.Count >= LobbyEntry.MaxSeats)
            {
                _logger.LogWarning("JoinLobby failed: lobby {LobbyId} is full.", lobbyId);
                return LobbyJoinOutcome.Fail(LobbyJoinFailureReason.LobbyFull);
            }

            if (entry.HasNickname(nickname))
            {
                _logger.LogWarning(
                    "JoinLobby failed: lobby {LobbyId} already has nickname '{Nickname}'.",
                    lobbyId, nickname);
                return LobbyJoinOutcome.Fail(LobbyJoinFailureReason.DuplicateNickname);
            }

            var seat = entry.AddSeat(nickname);

            _logger.LogInformation(
                "Lobby {LobbyId}: '{Nickname}' joined at seat {SeatIndex}.",
                lobbyId, nickname, seat.SeatIndex);

            var view = BuildView(entry);
            await _updatePublisher.PublishAsync(new LobbyUpdateEnvelope(lobbyId, entry.Revision, view));

            return LobbyJoinOutcome.Ok(new LobbyJoinResult(view, seat.SeatIndex, seat.ClientToken));
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    public async Task<LobbyStartOutcome> StartLobbyAsync(Guid lobbyId, string requestingClientToken)
    {
        var entry = _lobbyRepository.TryGet(lobbyId);
        if (entry is null)
        {
            _logger.LogWarning("StartLobby failed: lobby {LobbyId} not found.", lobbyId);
            return LobbyStartOutcome.Fail(LobbyStartFailureReason.LobbyNotFound);
        }

        await entry.Lock.WaitAsync();
        try
        {
            if (entry.IsStarted)
            {
                _logger.LogWarning("StartLobby failed: lobby {LobbyId} already started.", lobbyId);
                return LobbyStartOutcome.Fail(LobbyStartFailureReason.AlreadyStarted);
            }

            var adminSeat = entry.Seats.FirstOrDefault(s => s.SeatIndex == 0);
            if (adminSeat is null || adminSeat.ClientToken != requestingClientToken)
            {
                _logger.LogWarning("StartLobby failed: invalid admin token for lobby {LobbyId}.", lobbyId);
                return LobbyStartOutcome.Fail(LobbyStartFailureReason.NotAdmin);
            }

            // Validate the seat count here, before calling into GameApplicationService.CreateGameAsync
            // — that method throws ArgumentException outside the 2-4 range rather than returning a
            // result, which would otherwise surface as an unintended 500.
            if (entry.Seats.Count is < MinSeats or > MaxSeats)
            {
                _logger.LogWarning(
                    "StartLobby failed: lobby {LobbyId} has {SeatCount} seats (must be {Min}-{Max}).",
                    lobbyId, entry.Seats.Count, MinSeats, MaxSeats);
                return LobbyStartOutcome.Fail(LobbyStartFailureReason.InvalidSeatCount);
            }

            var playerNames = entry.Seats
                .OrderBy(s => s.SeatIndex)
                .Select(s => s.Nickname)
                .ToList();

            var creationResult = await _gameApplicationService.CreateGameAsync(playerNames);

            entry.MarkStarted(creationResult.GameId);

            _logger.LogInformation(
                "Lobby {LobbyId}: started, created game {GameId}.", lobbyId, creationResult.GameId);

            var view = BuildView(entry);
            await _updatePublisher.PublishAsync(new LobbyUpdateEnvelope(lobbyId, entry.Revision, view));

            return LobbyStartOutcome.Ok(new LobbyStartResult(creationResult.GameId));
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    private static LobbyView BuildView(LobbyEntry entry) =>
        new(
            entry.Id,
            entry.Seats.Select(s => new LobbySeatView(s.SeatIndex, s.Nickname)).ToList(),
            entry.IsStarted,
            entry.GameId);
}
