using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Sessions;
using TrashAnimal.Api.Startup.Options;
using TrashAnimal.Api.Updates;
using TrashAnimal.TokenPhase;

namespace TrashAnimal.Api.Application;

public sealed class GameApplicationService
{
    private readonly IGameSessionRepository _sessionRepository;
    private readonly IGameUpdatePublisher _updatePublisher;
    private readonly ILogger<GameApplicationService> _logger;
    private readonly GameApplicationServiceOptions _options;

    public GameApplicationService(
        IGameSessionRepository sessionRepository,
        IGameUpdatePublisher updatePublisher,
        ILogger<GameApplicationService> logger,
        IOptions<GameApplicationServiceOptions> options)
    {
        _sessionRepository = sessionRepository;
        _updatePublisher = updatePublisher;
        _logger = logger;
        _options = options.Value;
    }

    public Task<GameCreationResult> CreateGameAsync(IReadOnlyList<string> playerNames, int? dieSeed = null)
    {
        if (playerNames.Count is < 2 or > 4)
            throw new ArgumentException("Player count must be between 2 and 4.", nameof(playerNames));

        var players = playerNames
            .Select((name, index) => new Player(index, name))
            .ToList();

        var rng = dieSeed.HasValue ? new Random(dieSeed.Value) : null;
        var deck = new Deck();
        deck.ShuffleDeck(rng);

        var dealCounts = _options.StartingHandCounts.Take(playerNames.Count).ToList();
        deck.DealToPlayers(players, dealCounts);

        var session = new GameSession(players, deck);
        var die = new Die(rng);
        var entry = new GameSessionEntry(session, die);
        var gameId = Guid.NewGuid();

        _sessionRepository.Add(gameId, entry);

        _logger.LogInformation(
            "Game {GameId} created with {PlayerCount} players: {PlayerNames}.",
            gameId, players.Count, string.Join(", ", playerNames));

        var view = session.GetViewForPlayer(0);
        var allowedActions = session.GetAllowedActionsForPlayer(0);
        return Task.FromResult(new GameCreationResult(gameId, view, allowedActions));
    }

    public Task<(GameView View, IReadOnlyList<GameAction> AllowedActions, int Revision)?> GetViewAsync(
        Guid gameId,
        int playerSeat)
    {
        var entry = _sessionRepository.TryGet(gameId);
        if (entry is null)
        {
            _logger.LogWarning("GetView failed: game {GameId} not found.", gameId);
            return Task.FromResult<(GameView, IReadOnlyList<GameAction>, int)?>(null);
        }

        if (!IsValidPlayerSeat(entry.Session, playerSeat))
        {
            _logger.LogWarning(
                "GetView failed: invalid player seat {PlayerSeat} for game {GameId}.",
                playerSeat, gameId);
            return Task.FromResult<(GameView, IReadOnlyList<GameAction>, int)?>(null);
        }

        var view = entry.Session.GetViewForPlayer(playerSeat);
        var allowedActions = entry.Session.GetAllowedActionsForPlayer(playerSeat);
        return Task.FromResult<(GameView, IReadOnlyList<GameAction>, int)?>((view, allowedActions, entry.Revision));
    }

    /// <summary>
    /// Dispatches a command from the HTTP layer to the correct engine mutation, acquiring the
    /// per-session lock for the entire read-mutate-respond cycle to prevent concurrent corruption.
    /// </summary>
    public Task<GameCommandResult> DispatchCommandAsync(Guid gameId, SubmitCommandRequest request) =>
        WithSessionLockAsync(gameId, entry => ExecuteCommandUnlockedAsync(entry, gameId, request));

    public Task<GameCommandResult> SubmitActionAsync(Guid gameId, int playerSeat, GameAction action) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            if (!IsValidPlayerSeat(entry.Session, playerSeat))
                return GameCommandResult.Failure("Invalid player seat.");

            _logger.LogInformation(
                "Game {GameId}: player {PlayerSeat} submitting action {Action}.",
                gameId, playerSeat, action);

            var succeeded = entry.Session.ApplyAction(playerSeat, action, entry.Die, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<GameCommandResult> CompleteStealWithCardAsync(Guid gameId, int playerSeat, Guid cardId) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            _logger.LogInformation(
                "Game {GameId}: player {PlayerSeat} completing steal pick, card {CardId}.",
                gameId, playerSeat, cardId);

            var succeeded = entry.Session.TryCompleteStealWithCard(playerSeat, cardId, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<GameCommandResult> SubmitBanditPassAsync(Guid gameId, int playerSeat) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            _logger.LogInformation(
                "Game {GameId}: player {PlayerSeat} passing bandit.",
                gameId, playerSeat);

            var succeeded = entry.Session.TryBanditPass(playerSeat, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<GameCommandResult> SubmitBanditStashAsync(Guid gameId, int playerSeat, Guid cardId) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            _logger.LogInformation(
                "Game {GameId}: player {PlayerSeat} stashing card {CardId} for bandit.",
                gameId, playerSeat, cardId);

            var succeeded = entry.Session.TryBanditStashMatchingCard(playerSeat, cardId, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<GameCommandResult> SubmitDoubleStashAsync(
        Guid gameId,
        int playerSeat,
        IReadOnlyList<Guid> cardIds) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            _logger.LogInformation("Game {GameId}: player {PlayerSeat} submitting double stash ({CardCount} cards).", gameId, playerSeat, cardIds.Count);

            var succeeded = entry.Session.TryTokenPhaseDoubleStash(playerSeat, cardIds, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<GameCommandResult> SubmitStashTrashPickAsync(Guid gameId, int playerSeat, Guid cardId) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            _logger.LogInformation("Game {GameId}: player {PlayerSeat} picking card {CardId} for stash-trash.", gameId, playerSeat, cardId);

            var succeeded = entry.Session.TryTokenPhaseStashTrashPickCard(playerSeat, cardId, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<GameCommandResult> SubmitRecyclePickAsync(Guid gameId, int playerSeat, TokenAction replacement) =>
        WithSessionLockAsync(gameId, async entry =>
        {
            _logger.LogInformation("Game {GameId}: player {PlayerSeat} picking recycle replacement {Replacement}.", gameId, playerSeat, replacement);

            var succeeded = entry.Session.TryTokenPhaseRecyclePick(playerSeat, replacement, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        });

    public Task<IReadOnlyList<TokenAction>?> GetRecycleOptionsAsync(Guid gameId)
    {
        var entry = _sessionRepository.TryGet(gameId);
        if (entry is null)
        {
            _logger.LogWarning("GetRecycleOptions failed: game {GameId} not found.", gameId);
            return Task.FromResult<IReadOnlyList<TokenAction>?>(null);
        }

        return Task.FromResult<IReadOnlyList<TokenAction>?>(entry.Session.GetTokenPhaseRecycleOptions());
    }

    public Task<GameEndResult?> GetGameEndResultAsync(Guid gameId)
    {
        var entry = _sessionRepository.TryGet(gameId);
        if (entry is null)
        {
            _logger.LogWarning("GetGameEndResult failed: game {GameId} not found.", gameId);
            return Task.FromResult<GameEndResult?>(null);
        }

        if (entry.Session.State != GameState.GameEnded)
            return Task.FromResult<GameEndResult?>(null);

        return Task.FromResult<GameEndResult?>(entry.Session.GetGameEndResult());
    }

    private async Task<GameCommandResult> ExecuteCommandUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        SubmitCommandRequest request)
    {
        var playerSeat = request.PlayerSeat;

        if (!IsValidPlayerSeat(entry.Session, playerSeat))
            return GameCommandResult.Failure("Invalid player seat.");

        if (request.RecycleReplacement.HasValue)
        {
            _logger.LogInformation("Game {GameId}: player {PlayerSeat} picking recycle replacement {Replacement}.", gameId, playerSeat, request.RecycleReplacement.Value);

            var succeeded = entry.Session.TryTokenPhaseRecyclePick(playerSeat, request.RecycleReplacement.Value, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        }

        if (request.CardIds is { Count: > 0 })
        {
            _logger.LogInformation("Game {GameId}: player {PlayerSeat} submitting double stash ({CardCount} cards).", gameId, playerSeat, request.CardIds.Count);

            var succeeded = entry.Session.TryTokenPhaseDoubleStash(playerSeat, request.CardIds, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        }

        if (request.Action == GameAction.PlayFeesh)
        {
            if (!request.CardId.HasValue)
                return GameCommandResult.Failure("PlayFeesh requires CardId to specify which card to retrieve from discard pile.");

            _logger.LogInformation("Game {GameId}: player {PlayerSeat} playing Feesh to retrieve card {CardId}.", gameId, playerSeat, request.CardId.Value);

            var succeeded = entry.Session.TryPlayFeeshWithCardChoice(playerSeat, request.CardId.Value, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        }

        if (request.Action == GameAction.PlayShiny)
        {
            if (!request.VictimSeat.HasValue)
                return GameCommandResult.Failure("PlayShiny requires VictimSeat to specify which opponent to steal from.");

            _logger.LogInformation("Game {GameId}: player {PlayerSeat} playing Shiny to steal from player {VictimSeat}.", gameId, playerSeat, request.VictimSeat.Value);

            var succeeded = entry.Session.TryPlayShinyWithVictimChoice(playerSeat, request.VictimSeat.Value, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        }

        if (request.Action == GameAction.ResolveTokenSteal)
        {
            if (!request.VictimSeat.HasValue)
                return GameCommandResult.Failure("ResolveTokenSteal requires VictimSeat to specify which opponent to steal from.");

            _logger.LogInformation("Game {GameId}: player {PlayerSeat} resolving token steal against player {VictimSeat}.", gameId, playerSeat, request.VictimSeat.Value);

            var succeeded = entry.Session.TryStartTokenStealWithVictimChoice(playerSeat, request.VictimSeat.Value, out var error);
            return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
        }

        if (request.CardId.HasValue)
            return await ExecuteCardPickUnlockedAsync(entry, gameId, playerSeat, request.CardId.Value);

        _logger.LogInformation("Game {GameId}: player {PlayerSeat} submitting action {Action}.", gameId, playerSeat, request.Action);

        var actionSucceeded = entry.Session.ApplyAction(playerSeat, request.Action, entry.Die, out var actionError);
        return await BuildResultAsync(entry, gameId, playerSeat, actionSucceeded, actionError);
    }

    private async Task<GameCommandResult> ExecuteCardPickUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        Guid cardId)
    {
        bool succeeded;
        string? error;

        switch (entry.Session.State)
        {
            case GameState.AwaitingStealCardPick:
                _logger.LogInformation("Game {GameId}: player {PlayerSeat} completing steal pick, card {CardId}.", gameId, playerSeat, cardId);
                succeeded = entry.Session.TryCompleteStealWithCard(playerSeat, cardId, out error);
                break;

            case GameState.TokenPhase:
                var tokenStep = entry.Session.GetViewForPlayer(playerSeat).TokenPhase?.Step;
                switch (tokenStep)
                {
                    case TokenPhaseStep.StashTrashPickCard:
                        _logger.LogInformation("Game {GameId}: player {PlayerSeat} picking card {CardId} for stash-trash.", gameId, playerSeat, cardId);
                        succeeded = entry.Session.TryTokenPhaseStashTrashPickCard(playerSeat, cardId, out error);
                        break;

                    case TokenPhaseStep.BanditAwaitOpponentResponse:
                        _logger.LogInformation("Game {GameId}: player {PlayerSeat} stashing card {CardId} for bandit.", gameId, playerSeat, cardId);
                        succeeded = entry.Session.TryBanditStashMatchingCard(playerSeat, cardId, out error);
                        break;

                    default:
                        return GameCommandResult.Failure("A card pick is not expected in the current token phase step.");
                }
                break;

            default:
                return GameCommandResult.Failure("A card pick is not expected in the current game state.");
        }

        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
    }

    private async Task<GameCommandResult> WithSessionLockAsync(
        Guid gameId,
        Func<GameSessionEntry, Task<GameCommandResult>> operation)
    {
        var entry = _sessionRepository.TryGet(gameId);
        if (entry is null)
            return GameCommandResult.Failure("Game not found.");

        await entry.Lock.WaitAsync();
        try
        {
            return await operation(entry);
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    private async Task<GameCommandResult> BuildResultAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        bool succeeded,
        string? error)
    {
        if (!succeeded)
        {
            _logger.LogWarning("Game {GameId}: command rejected for player {PlayerSeat}. Reason: {Error}", gameId, playerSeat, error);
            return GameCommandResult.Failure(error ?? "Command rejected.");
        }

        var revision = entry.IncrementRevision();
        await _updatePublisher.PublishAsync(new GameUpdateEnvelope(gameId, revision, playerSeat, entry.Session.State));

        var view = entry.Session.GetViewForPlayer(playerSeat);
        var allowedActions = entry.Session.GetAllowedActionsForPlayer(playerSeat);
        return GameCommandResult.Ok(view, allowedActions);
    }

    private static bool IsValidPlayerSeat(GameSession session, int playerSeat) =>
        playerSeat >= 0 && playerSeat < session.Players.Count;
}
