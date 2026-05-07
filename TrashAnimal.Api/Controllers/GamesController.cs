using Microsoft.AspNetCore.Mvc;
using TrashAnimal.Api.Application;
using TrashAnimal.Api.Contracts.Requests;
using TrashAnimal.Api.Contracts.Responses;

namespace TrashAnimal.Api.Controllers;

[ApiController]
[Route("games")]
public sealed class GamesController : ControllerBase
{
    private readonly GameApplicationService _gameApplicationService;

    public GamesController(GameApplicationService gameApplicationService)
    {
        _gameApplicationService = gameApplicationService;
    }

    /// <summary>Creates a new game session and returns the game ID with the initial view for seat 0.</summary>
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

    /// <summary>
    /// Returns the per-player view, the set of currently allowed actions, and the session revision.
    /// </summary>
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

    /// <summary>
    /// Submits a game command. Use <see cref="SubmitCommandRequest.Action"/> as the primary discriminator;
    /// supply <see cref="SubmitCommandRequest.CardId"/>, <see cref="SubmitCommandRequest.CardIds"/>, or
    /// <see cref="SubmitCommandRequest.RecycleReplacement"/> when the action requires a payload.
    /// </summary>
    [HttpPost("{gameId:guid}/commands")]
    [ProducesResponseType(typeof(GameCommandResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GameCommandResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GameCommandResponse>> SubmitCommand(
        Guid gameId,
        [FromBody] SubmitCommandRequest request)
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

    /// <summary>
    /// Returns the final scoreboard. Only available after the game has reached
    /// <see cref="GameState.GameEnded"/>.
    /// </summary>
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
