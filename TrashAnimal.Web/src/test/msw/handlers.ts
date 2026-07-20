import { http, HttpResponse } from 'msw';
import { API_BASE_URL } from '../../api/httpClient';
import type {
  GameCommandResponse,
  GameCreationResponse,
  GameResultResponse,
  LobbyJoinResponse,
  LobbyStartResponse,
  LobbyView,
  PlayerViewResponse,
} from '../../api/types';

const DEFAULT_LOBBY: LobbyView = {
  lobbyId: '11111111-1111-1111-1111-111111111111',
  seats: [{ seatIndex: 0, nickname: 'Alice' }],
  isStarted: false,
  gameId: null,
};

const DEFAULT_GAME_VIEW: PlayerViewResponse = {
  view: {
    state: 'RollPhase',
    currentPlayerIndex: 0,
    currentPlayerName: 'Alice',
    isBusted: false,
    forcedRollRemaining: false,
    phaseOneTokens: [],
    handCardNames: ['Shiny', 'Feesh'],
    yumYumResponderIndex: null,
    yumYumResponderName: null,
    stealPhase: null,
    tokenPhase: null,
  },
  allowedActions: ['RollDie'],
  revision: 1,
};

/** Default success-path handlers, reused by Task 2's own tests and Task 5's component tests. */
export const handlers = [
  http.post(`${API_BASE_URL}/lobbies`, () =>
    HttpResponse.json<LobbyJoinResponse>(
      { lobby: DEFAULT_LOBBY, seatIndex: 0, clientToken: 'test-client-token' },
      { status: 201 },
    ),
  ),

  http.get(`${API_BASE_URL}/lobbies/:lobbyId`, () => HttpResponse.json<LobbyView>(DEFAULT_LOBBY)),

  http.post(`${API_BASE_URL}/lobbies/:lobbyId/players`, () =>
    HttpResponse.json<LobbyJoinResponse>({
      lobby: {
        ...DEFAULT_LOBBY,
        seats: [...DEFAULT_LOBBY.seats, { seatIndex: 1, nickname: 'Bob' }],
      },
      seatIndex: 1,
      clientToken: 'test-client-token-2',
    }),
  ),

  http.post(`${API_BASE_URL}/lobbies/:lobbyId/start`, () =>
    HttpResponse.json<LobbyStartResponse>({ gameId: '22222222-2222-2222-2222-222222222222' }),
  ),

  http.post(`${API_BASE_URL}/games`, () =>
    HttpResponse.json<GameCreationResponse>(
      {
        gameId: '22222222-2222-2222-2222-222222222222',
        view: DEFAULT_GAME_VIEW.view,
        allowedActions: DEFAULT_GAME_VIEW.allowedActions,
      },
      { status: 201 },
    ),
  ),

  http.get(`${API_BASE_URL}/games/:gameId/view`, () =>
    HttpResponse.json<PlayerViewResponse>(DEFAULT_GAME_VIEW),
  ),

  http.post(`${API_BASE_URL}/games/:gameId/commands`, () =>
    HttpResponse.json<GameCommandResponse>({
      succeeded: true,
      errorMessage: null,
      view: DEFAULT_GAME_VIEW.view,
      allowedActions: DEFAULT_GAME_VIEW.allowedActions,
    }),
  ),

  http.get(`${API_BASE_URL}/games/:gameId/result`, () =>
    HttpResponse.json<GameResultResponse>({
      scoreLines: [{ playerIndex: 0, playerName: 'Alice', totalScore: 12 }],
      winningPlayerIndex: 0,
    }),
  ),
];
