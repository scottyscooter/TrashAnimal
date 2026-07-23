import { getJson, postJson } from './httpClient';
import type {
  CreateGameRequest,
  GameCommandRequest,
  GameCommandResponse,
  GameCreationResponse,
  GameResultResponse,
  PlayerViewResponse,
} from './types';

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
   * without a try/catch.
   *
   * Sends GameCommandRequest (polymorphic union with `kind` discriminator).
   */
  submitCommand(gameId: string, request: GameCommandRequest): Promise<GameCommandResponse> {
    return postJson<GameCommandResponse>(
      `/games/${encodeURIComponent(gameId)}/commands`,
      request,
      { expectedStatuses: [422] },
    );
  },

  getResult(gameId: string): Promise<GameResultResponse> {
    return getJson<GameResultResponse>(`/games/${encodeURIComponent(gameId)}/result`);
  },
};
