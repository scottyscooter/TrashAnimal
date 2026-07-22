using Microsoft.AspNetCore.Mvc;
using TrashAnimal.Api.Application;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Controllers;

[ApiController]
[Route("games")]
[Produces("application/json")]
public sealed class GamesController : ControllerBase
{
    private readonly GameApplicationService _gameApplicationService;

    public GamesController(GameApplicationService gameApplicationService)
    {
        _gameApplicationService = gameApplicationService;
    }

    /// <summary>Creates a new game session.</summary>
    /// <remarks>
    /// Request body:
    /// <code>
    /// {
    ///   "playerNames": ["Alice", "Bob"],
    ///   "dieSeed": 42          // optional; omit for a random die
    /// }
    /// </code>
    ///
    /// 201 response body:
    /// <code>
    /// {
    ///   "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///   "view": { ...GameView for seat 0... },
    ///   "allowedActions": ["RollDie"]
    /// }
    /// </code>
    ///
    /// The <c>Location</c> header points to <c>GET /games/{gameId}/view?playerSeat=0</c>.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(GameCreationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GameCreationResponse>> CreateGame([FromBody] CreateGameRequest request)
    {
        if (request.PlayerNames is not { Count: >= 2 and <= 4 })
            return BadRequest("PlayerNames must contain between 2 and 4 names.");

        var result = await _gameApplicationService.CreateGameAsync(request.PlayerNames, request.DieSeed);
        var response = new GameCreationResponse(result.GameId, result.View, result.AllowedActions);
        return CreatedAtAction(nameof(GetView), new { gameId = result.GameId, playerSeat = 0 }, response);
    }

    /// <summary>Returns the per-player game view for the specified seat.</summary>
    /// <remarks>
    /// Query parameters:
    /// <list type="bullet">
    ///   <item><c>playerSeat</c> (int, required) — zero-based seat index of the requesting player.</item>
    /// </list>
    ///
    /// 200 response body:
    /// <code>
    /// {
    ///   "view": {
    ///     "state": "RollPhase",
    ///     "currentPlayerIndex": 0,
    ///     "currentPlayerName": "Alice",
    ///     "isBusted": false,
    ///     "forcedRollRemaining": false,
    ///     "phaseOneTokens": ["StashTrash"],
    ///     "handCardNames": ["Shiny", "Feesh"],
    ///     "yumYumResponderIndex": null,
    ///     "yumYumResponderName": null,
    ///     "stealPhase": null,
    ///     "tokenPhase": null
    ///   },
    ///   "allowedActions": ["RollDie", "StopRolling"],
    ///   "revision": 3
    /// }
    /// </code>
    ///
    /// <c>handCardNames</c> contains only the requesting player's own cards.
    /// Opponent hand contents are never included (see hidden-information constraint).
    /// Poll this endpoint after receiving a <c>GameUpdated</c> SignalR notification.
    /// </remarks>
    [HttpGet("{gameId:guid}/view")]
    [ProducesResponseType(typeof(PlayerViewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PlayerViewResponse>> GetView(Guid gameId, [FromQuery] int playerSeat)
    {
        var result = await _gameApplicationService.GetViewAsync(gameId, playerSeat);
        if (result is null)
            return NotFound();

        var (view, allowedActions, revision) = result.Value;
        return Ok(new PlayerViewResponse(view, allowedActions, revision));
    }

    /// <summary>Submits a game command for a player.</summary>
    /// <remarks>
    /// The request body is a discriminated union keyed by <c>kind</c>:
    ///
    /// | Scenario | <c>kind</c> value | Payload fields |
    /// |---|---|---|
    /// | Standard game action | <c>"action"</c> | <c>action</c> (GameAction) |
    /// | Play Feesh card | <c>"playFeesh"</c> | <c>cardId</c> (Guid) |
    /// | Play Shiny card | <c>"playShiny"</c> | <c>victimSeat</c> (int) |
    /// | Resolve token steal | <c>"resolveTokenSteal"</c> | <c>victimSeat</c> (int) |
    /// | Card pick (context-dependent) | <c>"cardPick"</c> | <c>cardId</c> (Guid) |
    /// | Double stash submit | <c>"doubleStash"</c> | <c>cardIds</c> (Guid[]) |
    /// | Recycle pick | <c>"recyclePick"</c> | <c>replacement</c> (TokenAction) |
    ///
    /// 200 response body (success):
    /// <code>
    /// {
    ///   "succeeded": true,
    ///   "errorMessage": null,
    ///   "view": { ...updated GameView for the acting player... },
    ///   "allowedActions": ["EndTurn"]
    /// }
    /// </code>
    ///
    /// 422 response body (game rule rejection):
    /// <code>
    /// {
    ///   "succeeded": false,
    ///   "errorMessage": "Action is not allowed right now.",
    ///   "view": null,
    ///   "allowedActions": null
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("{gameId:guid}/commands")]
    [ProducesResponseType(typeof(GameCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GameCommandResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameCommandResponse>> SubmitCommand(
        Guid gameId,
        [FromBody] GameCommandRequest request)
    {
        var result = await _gameApplicationService.DispatchCommandAsync(gameId, request);

        if (!result.Success)
        {
            if (result.ErrorMessage == "Game not found.")
                return NotFound();

            return UnprocessableEntity(GameCommandResponse.FromFailure(result.ErrorMessage!));
        }

        return Ok(GameCommandResponse.FromSuccess(result.View!, result.AllowedActions!));
    }

    /// <summary>Returns the final scoreboard for a completed game.</summary>
    /// <remarks>
    /// Only available after the game has reached <c>GameState.GameEnded</c>.
    /// Returns 404 both when the game does not exist and when it has not yet ended.
    ///
    /// 200 response body:
    /// <code>
    /// {
    ///   "scoreLines": [
    ///     { "playerIndex": 0, "playerName": "Alice", "totalScore": 12 },
    ///     { "playerIndex": 1, "playerName": "Bob",   "totalScore":  9 }
    ///   ],
    ///   "winningPlayerIndex": 0
    /// }
    /// </code>
    /// </remarks>
    [HttpGet("{gameId:guid}/result")]
    [ProducesResponseType(typeof(GameResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameResultResponse>> GetResult(Guid gameId)
    {
        var result = await _gameApplicationService.GetGameEndResultAsync(gameId);
        if (result is null)
            return NotFound();

        return Ok(new GameResultResponse(result.ScoreLines, result.WinningPlayerIndex));
    }
}
