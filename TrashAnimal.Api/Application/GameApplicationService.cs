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
    public Task<GameCommandResult> DispatchCommandAsync(Guid gameId, GameCommandRequest request) =>
        WithSessionLockAsync(gameId, entry => ExecuteUnlockedCommandAsync(entry, gameId, request));

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

    private async Task<GameCommandResult> ExecuteUnlockedCommandAsync(
        GameSessionEntry entry,
        Guid gameId,
        GameCommandRequest request)
    {
        var playerSeat = request.PlayerSeat;

        if (!IsValidPlayerSeat(entry.Session, playerSeat))
            return GameCommandResult.Failure("Invalid player seat.");

        return request switch
        {
            PlayActionCommand cmd => await ExecutePlayActionUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.Action),
            PlayFeeshCommand cmd => await ExecutePlayFeeshUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.CardId),
            PlayShinyCommand cmd => await ExecutePlayShinyUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.VictimSeat),
            ResolveTokenStealCommand cmd => await ExecuteTokenStealUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.VictimSeat),
            CardPickCommand cmd => await ExecuteCardPickUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.CardId),
            DoubleStashCommand cmd => await ExecuteDoubleStashUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.CardIds),
            RecyclePickCommand cmd => await ExecuteRecyclePickUnlockedAsync(entry, gameId, cmd.PlayerSeat, cmd.Replacement),
            _ => GameCommandResult.Failure("Unknown command type.")
        };
    }

    private async Task<GameCommandResult> ExecutePlayActionUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        GameAction action)
    {
        _logger.LogInformation(
            "Game {GameId}: player {PlayerSeat} submitting action {Action}.",
            gameId, playerSeat, action);

        var succeeded = entry.Session.ApplyAction(playerSeat, action, entry.Die, out var error);
        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
    }

    private async Task<GameCommandResult> ExecutePlayFeeshUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        Guid cardId)
    {
        _logger.LogInformation("Game {GameId}: player {PlayerSeat} playing Feesh to retrieve card {CardId}.", gameId, playerSeat, cardId);

        var succeeded = entry.Session.TryPlayFeeshWithCardChoice(playerSeat, cardId, out var error);
        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
    }

    private async Task<GameCommandResult> ExecutePlayShinyUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        int victimSeat)
    {
        _logger.LogInformation("Game {GameId}: player {PlayerSeat} playing Shiny to steal from player {VictimSeat}.", gameId, playerSeat, victimSeat);

        var succeeded = entry.Session.TryPlayShinyWithVictimChoice(playerSeat, victimSeat, out var error);
        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
    }

    private async Task<GameCommandResult> ExecuteTokenStealUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        int victimSeat)
    {
        _logger.LogInformation("Game {GameId}: player {PlayerSeat} resolving token steal against player {VictimSeat}.", gameId, playerSeat, victimSeat);

        var succeeded = entry.Session.TryStartTokenStealWithVictimChoice(playerSeat, victimSeat, out var error);
        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
    }

    private async Task<GameCommandResult> ExecuteDoubleStashUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        IReadOnlyList<Guid> cardIds)
    {
        _logger.LogInformation("Game {GameId}: player {PlayerSeat} submitting double stash ({CardCount} cards).", gameId, playerSeat, cardIds.Count);

        var succeeded = entry.Session.TryTokenPhaseDoubleStash(playerSeat, cardIds, out var error);
        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
    }

    private async Task<GameCommandResult> ExecuteRecyclePickUnlockedAsync(
        GameSessionEntry entry,
        Guid gameId,
        int playerSeat,
        TokenAction replacement)
    {
        _logger.LogInformation("Game {GameId}: player {PlayerSeat} picking recycle replacement {Replacement}.", gameId, playerSeat, replacement);

        var succeeded = entry.Session.TryTokenPhaseRecyclePick(playerSeat, replacement, out var error);
        return await BuildResultAsync(entry, gameId, playerSeat, succeeded, error);
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
