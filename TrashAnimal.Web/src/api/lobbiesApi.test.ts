import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { API_BASE_URL } from './httpClient';
import { ApiError } from './httpClient';
import { lobbiesApi } from './lobbiesApi';

describe('lobbiesApi', () => {
  it('createLobby posts a nickname and returns the seated response', async () => {
    const response = await lobbiesApi.createLobby({ nickname: 'Alice' });
    expect(response.seatIndex).toBe(0);
    expect(response.clientToken).toBe('test-client-token');
  });

  it('getLobby fetches the current lobby view', async () => {
    const view = await lobbiesApi.getLobby('lobby-1');
    expect(view.seats).toHaveLength(1);
  });

  it('joinLobby seats the caller at the next available index', async () => {
    const response = await lobbiesApi.joinLobby('lobby-1', { nickname: 'Bob' });
    expect(response.seatIndex).toBe(1);
  });

  it('startLobby returns the created gameId', async () => {
    const response = await lobbiesApi.startLobby('lobby-1', { clientToken: 'test-client-token' });
    expect(response.gameId).toBe('22222222-2222-2222-2222-222222222222');
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
