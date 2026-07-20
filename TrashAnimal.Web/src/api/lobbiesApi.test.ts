import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { API_BASE_URL } from './httpClient';
import { ApiError } from './httpClient';
import { lobbiesApi } from './lobbiesApi';

describe('lobbiesApi', () => {
  it('createLobby posts the nickname as the request body', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/lobbies`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          {
            lobby: { lobbyId: 'lobby-1', seats: [{ seatIndex: 0, nickname: 'Alice' }], isStarted: false, gameId: null },
            seatIndex: 0,
            clientToken: 'test-client-token',
          },
          { status: 201 },
        );
      }),
    );

    const response = await lobbiesApi.createLobby({ nickname: 'Alice' });
    expect(capturedBody).toEqual({ nickname: 'Alice' });
    expect(response.seatIndex).toBe(0);
    expect(response.clientToken).toBe('test-client-token');
  });

  it('getLobby fetches the current lobby view from the given lobbyId', async () => {
    let capturedPath: string | undefined;
    server.use(
      http.get(`${API_BASE_URL}/lobbies/:lobbyId`, ({ request }) => {
        capturedPath = new URL(request.url).pathname;
        return HttpResponse.json({ lobbyId: 'lobby-1', seats: [], isStarted: false, gameId: null });
      }),
    );

    const view = await lobbiesApi.getLobby('lobby-1');
    expect(capturedPath).toBe('/lobbies/lobby-1');
    expect(view.lobbyId).toBe('lobby-1');
  });

  it('joinLobby posts the nickname to the lobby-scoped players route', async () => {
    let capturedBody: unknown;
    let capturedPath: string | undefined;
    server.use(
      http.post(`${API_BASE_URL}/lobbies/:lobbyId/players`, async ({ request }) => {
        capturedBody = await request.json();
        capturedPath = new URL(request.url).pathname;
        return HttpResponse.json({
          lobby: {
            lobbyId: 'lobby-1',
            seats: [
              { seatIndex: 0, nickname: 'Alice' },
              { seatIndex: 1, nickname: 'Bob' },
            ],
            isStarted: false,
            gameId: null,
          },
          seatIndex: 1,
          clientToken: 'test-client-token-2',
        });
      }),
    );

    const response = await lobbiesApi.joinLobby('lobby-1', { nickname: 'Bob' });
    expect(capturedPath).toBe('/lobbies/lobby-1/players');
    expect(capturedBody).toEqual({ nickname: 'Bob' });
    expect(response.seatIndex).toBe(1);
  });

  it('startLobby posts the clientToken to the lobby-scoped start route', async () => {
    let capturedBody: unknown;
    let capturedPath: string | undefined;
    server.use(
      http.post(`${API_BASE_URL}/lobbies/:lobbyId/start`, async ({ request }) => {
        capturedBody = await request.json();
        capturedPath = new URL(request.url).pathname;
        return HttpResponse.json({ gameId: '22222222-2222-2222-2222-222222222222' });
      }),
    );

    const response = await lobbiesApi.startLobby('lobby-1', { clientToken: 'test-client-token' });
    expect(capturedPath).toBe('/lobbies/lobby-1/start');
    expect(capturedBody).toEqual({ clientToken: 'test-client-token' });
    expect(response.gameId).toBe('22222222-2222-2222-2222-222222222222');
  });

  it('URL-encodes the lobbyId path segment', async () => {
    let capturedPath: string | undefined;
    server.use(
      http.get(`${API_BASE_URL}/lobbies/:lobbyId`, ({ request }) => {
        capturedPath = new URL(request.url).pathname;
        return HttpResponse.json({ lobbyId: 'lobby/with slash', seats: [], isStarted: false, gameId: null });
      }),
    );

    await lobbiesApi.getLobby('lobby/with slash');
    expect(capturedPath).toBe(`/lobbies/${encodeURIComponent('lobby/with slash')}`);
  });

  it.each([
    { status: 400, body: 'Nickname must not be empty.' },
    { status: 403, body: 'Only the lobby admin can start the game.' },
    { status: 409, body: 'Lobby is full.' },
    { status: 422, body: 'Lobby must have between 2 and 4 players to start.' },
  ])('throws ApiError uniformly for a $status rejection', async ({ status, body }) => {
    server.use(
      http.post(`${API_BASE_URL}/lobbies/:lobbyId/start`, () => HttpResponse.text(body, { status })),
    );

    await expect(lobbiesApi.startLobby('lobby-1', { clientToken: 'bad-token' })).rejects.toBeInstanceOf(ApiError);
  });
});
