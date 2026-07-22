import { getJson, postJson } from './httpClient';
import type {
  CreateGameRequest,
  GameCommandResponse,
  GameCreationResponse,
  GameResultResponse,
  PlayerViewResponse,
  SubmitCommandRequest,
  SubmitCommandRequestWire,
} from './types';

/**
 * A GameAction filler value used for the contextual command variants (card picks, double stash,
 * recycle pick) whose wire `action` field GamesController's dispatcher ignores — it routes those
 * requests by whichever of `recycleReplacement`/`cardIds`/`cardId` is populated and the session's
 * current GameState/TokenPhaseStep instead. See GameApplicationService.ExecuteUnlockedCommandAsync.
 */
const CONTEXTUAL_ACTION_FILLER = 'EndTurn';

function toWireRequest(request: SubmitCommandRequest): SubmitCommandRequestWire {
  switch (request.kind) {
    case 'action':
      return { playerSeat: request.playerSeat, action: request.action };
    case 'playFeesh':
      return { playerSeat: request.playerSeat, action: 'PlayFeesh', cardId: request.cardId };
    case 'playShiny':
      return { playerSeat: request.playerSeat, action: 'PlayShiny', victimSeat: request.victimSeat };
    case 'resolveTokenSteal':
      return {
        playerSeat: request.playerSeat,
        action: 'ResolveTokenSteal',
        victimSeat: request.victimSeat,
      };
    case 'stealCardPick':
    case 'stashTrashCardPick':
    case 'banditStashCardPick':
      return {
        playerSeat: request.playerSeat,
        action: CONTEXTUAL_ACTION_FILLER,
        cardId: request.cardId,
      };
    case 'doubleStashSubmit':
      return {
        playerSeat: request.playerSeat,
        action: CONTEXTUAL_ACTION_FILLER,
        cardIds: request.cardIds,
      };
    case 'recyclePick':
      return {
        playerSeat: request.playerSeat,
        action: CONTEXTUAL_ACTION_FILLER,
        recycleReplacement: request.recycleReplacement,
      };
  }
}

export const gamesApi = {
  createGame(request: CreateGameRequest): Promise<GameCreationResponse> {
    return postJson<GameCreationResponse>('/games', request);
  },

  getView(gameId: string, playerSeat: number): Promise<PlayerViewResponse> {
    return getJson<PlayerViewResponse>(
      `/games/${encodeURIComponent(gameId)}/view?playerSeat=${encodeURIComponent(playerSeat)}`,
    );
  },

  /**
   * Returns the parsed GameCommandResponse for both success and the 422 rule-rejection case
   * (Succeeded: false) rather than throwing — GamesController's 422 is the only expected-rejection
   * status here and always carries the structured envelope, so callers can branch on `succeeded`
   * without a try/catch (review note 1).
   */
  submitCommand(gameId: string, request: SubmitCommandRequest): Promise<GameCommandResponse> {
    return postJson<GameCommandResponse>(
      `/games/${encodeURIComponent(gameId)}/commands`,
      toWireRequest(request),
      { expectedStatuses: [422] },
    );
  },

  getResult(gameId: string): Promise<GameResultResponse> {
    return getJson<GameResultResponse>(`/games/${encodeURIComponent(gameId)}/result`);
  },
};
