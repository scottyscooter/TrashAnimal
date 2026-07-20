import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { ApiError, API_BASE_URL, getJson, postJson } from './httpClient';

describe('httpClient', () => {
  it('parses a JSON error body and throws ApiError with the envelope message', async () => {
    server.use(
      http.post(
        `${API_BASE_URL}/games/:gameId/commands`,
        () =>
          HttpResponse.json(
            { succeeded: false, errorMessage: 'Action is not allowed right now.', view: null, allowedActions: null },
            { status: 422 },
          ),
      ),
    );

    await expect(postJson('/games/abc/commands', { playerSeat: 0, action: 'EndTurn' })).rejects.toMatchObject({
      status: 422,
      message: 'Action is not allowed right now.',
    });
  });

  it('parses a bare-text error body and throws ApiError with that text as the message', async () => {
    server.use(
      http.post(`${API_BASE_URL}/lobbies/:lobbyId/start`, () =>
        HttpResponse.text('Only the lobby admin can start the game.', { status: 403 }),
      ),
    );

    await expect(postJson('/lobbies/abc/start', { clientToken: 'x' })).rejects.toMatchObject({
      status: 403,
      message: 'Only the lobby admin can start the game.',
    });
  });

  it('resolves an expected status without throwing when listed in expectedStatuses', async () => {
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, () =>
        HttpResponse.json({ succeeded: false, errorMessage: 'Rejected.', view: null, allowedActions: null }, { status: 422 }),
      ),
    );

    const result = await postJson<{ succeeded: boolean }>(
      '/games/abc/commands',
      { playerSeat: 0, action: 'EndTurn' },
      { expectedStatuses: [422] },
    );
    expect(result.succeeded).toBe(false);
  });

  it('resolves a successful GET request', async () => {
    server.use(
      http.get(`${API_BASE_URL}/lobbies/:lobbyId`, () =>
        HttpResponse.json({ lobbyId: 'abc', seats: [], isStarted: false, gameId: null }),
      ),
    );

    const result = await getJson<{ lobbyId: string }>('/lobbies/abc');
    expect(result.lobbyId).toBe('abc');
  });

  it('ApiError carries the parsed body for callers that need structured detail', async () => {
    server.use(
      http.post(`${API_BASE_URL}/lobbies`, () => HttpResponse.text('Nickname must not be empty.', { status: 400 })),
    );

    try {
      await postJson('/lobbies', { nickname: '' });
      expect.unreachable('expected postJson to throw');
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError);
      expect((error as ApiError).body).toBe('Nickname must not be empty.');
    }
  });
});
