using Microsoft.AspNetCore.Mvc;
using TrashAnimal.Api.Application;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Controllers;

[ApiController]
[Route("lobbies")]
[Produces("application/json")]
public sealed class LobbiesController : ControllerBase
{
    private const int MaxNicknameLength = 24;

    private readonly LobbyApplicationService _lobbyApplicationService;

    public LobbiesController(LobbyApplicationService lobbyApplicationService)
    {
        _lobbyApplicationService = lobbyApplicationService;
    }

    /// <summary>Creates a new lobby. The caller becomes the admin, seated at index 0.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LobbyJoinResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LobbyJoinResponse>> CreateLobby([FromBody] CreateLobbyRequest request)
    {
        if (!TryValidateNickname(request.Nickname, out var nickname, out var error))
            return BadRequest(error);

        var result = await _lobbyApplicationService.CreateLobbyAsync(nickname);
        var response = new LobbyJoinResponse(result.Lobby, result.SeatIndex, result.ClientToken);
        return CreatedAtAction(nameof(GetLobby), new { lobbyId = result.Lobby.LobbyId }, response);
    }

    /// <summary>Returns the current lobby state. Polling fallback / initial load for the Lobby page.</summary>
    [HttpGet("{lobbyId:guid}")]
    [ProducesResponseType(typeof(LobbyView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LobbyView>> GetLobby(Guid lobbyId)
    {
        var view = await _lobbyApplicationService.GetLobbyViewAsync(lobbyId);
        if (view is null)
            return NotFound();

        return Ok(view);
    }

    /// <summary>Joins an existing lobby at the next available seat.</summary>
    [HttpPost("{lobbyId:guid}/players")]
    [ProducesResponseType(typeof(LobbyJoinResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LobbyJoinResponse>> JoinLobby(Guid lobbyId, [FromBody] JoinLobbyRequest request)
    {
        if (!TryValidateNickname(request.Nickname, out var nickname, out var error))
            return BadRequest(error);

        var outcome = await _lobbyApplicationService.JoinLobbyAsync(lobbyId, nickname);
        if (!outcome.Success)
        {
            return outcome.FailureReason switch
            {
                LobbyJoinFailureReason.LobbyNotFound => NotFound(),
                LobbyJoinFailureReason.LobbyAlreadyStarted => Conflict("Lobby has already started."),
                LobbyJoinFailureReason.LobbyFull => Conflict("Lobby is full."),
                LobbyJoinFailureReason.DuplicateNickname => Conflict("Nickname is already taken in this lobby."),
                _ => Conflict(),
            };
        }

        var result = outcome.Result!;
        return Ok(new LobbyJoinResponse(result.Lobby, result.SeatIndex, result.ClientToken));
    }

    /// <summary>Admin-only: starts the lobby, creating a real playable game from the current roster.</summary>
    [HttpPost("{lobbyId:guid}/start")]
    [ProducesResponseType(typeof(LobbyStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LobbyStartResponse>> StartLobby(Guid lobbyId, [FromBody] StartLobbyRequest request)
    {
        var outcome = await _lobbyApplicationService.StartLobbyAsync(lobbyId, request.ClientToken);
        if (!outcome.Success)
        {
            return outcome.FailureReason switch
            {
                LobbyStartFailureReason.LobbyNotFound => NotFound(),
                LobbyStartFailureReason.NotAdmin => StatusCode(StatusCodes.Status403Forbidden, "Only the lobby admin can start the game."),
                LobbyStartFailureReason.InvalidSeatCount => UnprocessableEntity("Lobby must have between 2 and 4 players to start."),
                LobbyStartFailureReason.AlreadyStarted => Conflict("Lobby has already started."),
                _ => Conflict(),
            };
        }

        return Ok(new LobbyStartResponse(outcome.Result!.GameId));
    }

    /// <summary>
    /// Enforces nickname presence and length at the transport boundary — mirrors how
    /// <see cref="GamesController.CreateGame"/> validates <c>PlayerNames</c> count.
    /// </summary>
    private static bool TryValidateNickname(string? nickname, out string trimmed, out string? error)
    {
        trimmed = nickname?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
        {
            error = "Nickname must not be empty.";
            return false;
        }

        if (trimmed.Length > MaxNicknameLength)
        {
            error = $"Nickname must be {MaxNicknameLength} characters or fewer.";
            return false;
        }

        error = null;
        return true;
    }
}
