import { getJson, postJson } from './httpClient';
import type {
  CreateLobbyRequest,
  JoinLobbyRequest,
  LobbyJoinResponse,
  LobbyStartResponse,
  LobbyView,
  StartLobbyRequest,
} from './types';

/**
 * Unlike gamesApi, every expected-rejection status here (400/403/409/422) throws `ApiError`
 * uniformly — LobbiesController returns bare strings for all of them (no JSON envelope), and
 * they're form-validation/conflict outcomes rather than a live-game state race (review note 1).
 */
export const lobbiesApi = {
  createLobby(request: CreateLobbyRequest): Promise<LobbyJoinResponse> {
    return postJson<LobbyJoinResponse>('/lobbies', request);
  },

  getLobby(lobbyId: string): Promise<LobbyView> {
    return getJson<LobbyView>(`/lobbies/${encodeURIComponent(lobbyId)}`);
  },

  joinLobby(lobbyId: string, request: JoinLobbyRequest): Promise<LobbyJoinResponse> {
    return postJson<LobbyJoinResponse>(`/lobbies/${encodeURIComponent(lobbyId)}/players`, request);
  },

  startLobby(lobbyId: string, request: StartLobbyRequest): Promise<LobbyStartResponse> {
    return postJson<LobbyStartResponse>(`/lobbies/${encodeURIComponent(lobbyId)}/start`, request);
  },
};
