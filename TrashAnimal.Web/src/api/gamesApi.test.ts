import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { API_BASE_URL } from './httpClient';
import { gamesApi } from './gamesApi';
import type { GameCommandRequest } from './types';

describe('gamesApi', () => {
  it('createGame posts playerNames as the request body', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json(
          { gameId: '22222222-2222-2222-2222-222222222222', view: {}, allowedActions: [] },
          { status: 201 },
        );
      }),
    );

    const response = await gamesApi.createGame({ playerNames: ['Alice', 'Bob'], dieSeed: 42 });
    expect(capturedBody).toEqual({ playerNames: ['Alice', 'Bob'], dieSeed: 42 });
    expect(response.gameId).toBe('22222222-2222-2222-2222-222222222222');
  });

  it('getView requests the given playerSeat as a query parameter', async () => {
    let capturedUrl: URL | undefined;
    server.use(
      http.get(`${API_BASE_URL}/games/:gameId/view`, ({ request }) => {
        capturedUrl = new URL(request.url);
        return HttpResponse.json({ view: {}, allowedActions: [], revision: 1 });
      }),
    );

    await gamesApi.getView('22222222-2222-2222-2222-222222222222', 2);
    expect(capturedUrl?.pathname).toBe('/games/22222222-2222-2222-2222-222222222222/view');
    expect(capturedUrl?.searchParams.get('playerSeat')).toBe('2');
  });

  it('submitCommand sends PlayActionCommand with kind: "action"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'action', playerSeat: 0, action: 'RollDie' };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand sends PlayFeeshCommand with kind: "playFeesh"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'playFeesh', playerSeat: 1, cardId: 'card-1' };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand sends DoubleStashCommand with kind: "doubleStash"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'doubleStash', playerSeat: 0, cardIds: ['card-a', 'card-b'] };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand sends PlayShinyCommand with kind: "playShiny"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'playShiny', playerSeat: 0, victimSeat: 2 };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand sends ResolveTokenStealCommand with kind: "resolveTokenSteal"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'resolveTokenSteal', playerSeat: 1, victimSeat: 3 };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand sends CardPickCommand with kind: "cardPick"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'cardPick', playerSeat: 0, cardId: 'card-2' };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand sends RecyclePickCommand with kind: "recyclePick"', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    const request: GameCommandRequest = { kind: 'recyclePick', playerSeat: 0, replacement: 'Bandit' };
    await gamesApi.submitCommand('game-1', request);
    expect(capturedBody).toEqual(request);
  });

  it('submitCommand and getResult URL-encode the gameId path segment', async () => {
    let capturedPath: string | undefined;
    server.use(
      http.get(`${API_BASE_URL}/games/:gameId/result`, ({ request }) => {
        capturedPath = new URL(request.url).pathname;
        return HttpResponse.json({ scoreLines: [], winningPlayerIndex: 0 });
      }),
    );

    await gamesApi.getResult('game/with slash');
    expect(capturedPath).toBe(`/games/${encodeURIComponent('game/with slash')}/result`);
  });

  it('submitCommand resolves (does not throw) on a 422 rule rejection', async () => {
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, () =>
        HttpResponse.json(
          { succeeded: false, errorMessage: 'Action is not allowed right now.', view: null, allowedActions: null },
          { status: 422 },
        ),
      ),
    );

    const request: GameCommandRequest = { kind: 'action', playerSeat: 0, action: 'EndTurn' };
    const response = await gamesApi.submitCommand('game-1', request);
    expect(response.succeeded).toBe(false);
    expect(response.errorMessage).toBe('Action is not allowed right now.');
  });

  it('getResult fetches the final scoreboard', async () => {
    const result = await gamesApi.getResult('game-1');
    expect(result.winningPlayerIndex).toBe(0);
    expect(result.scoreLines).toHaveLength(1);
  });
});
