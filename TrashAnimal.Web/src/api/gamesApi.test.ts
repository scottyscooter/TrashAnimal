import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { API_BASE_URL } from './httpClient';
import { gamesApi } from './gamesApi';

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

  it('submitCommand sends a plain action as the wire shape', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'action', playerSeat: 0, action: 'RollDie' });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'RollDie' });
  });

  it('submitCommand translates playFeesh into action + cardId', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'playFeesh', playerSeat: 1, cardId: 'card-1' });
    expect(capturedBody).toEqual({ playerSeat: 1, action: 'PlayFeesh', cardId: 'card-1' });
  });

  it('submitCommand translates doubleStashSubmit into cardIds without a meaningful action', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', {
      kind: 'doubleStashSubmit',
      playerSeat: 0,
      cardIds: ['card-a', 'card-b'],
    });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'EndTurn', cardIds: ['card-a', 'card-b'] });
  });

  it('submitCommand translates playShiny into action + victimSeat', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'playShiny', playerSeat: 0, victimSeat: 2 });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'PlayShiny', victimSeat: 2 });
  });

  it('submitCommand translates resolveTokenSteal into action + victimSeat', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'resolveTokenSteal', playerSeat: 1, victimSeat: 3 });
    expect(capturedBody).toEqual({ playerSeat: 1, action: 'ResolveTokenSteal', victimSeat: 3 });
  });

  it('submitCommand translates stealCardPick into a bare cardId without a meaningful action', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'stealCardPick', playerSeat: 0, cardId: 'card-2' });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'EndTurn', cardId: 'card-2' });
  });

  it('submitCommand translates stashTrashCardPick into a bare cardId without a meaningful action', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'stashTrashCardPick', playerSeat: 0, cardId: 'card-3' });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'EndTurn', cardId: 'card-3' });
  });

  it('submitCommand translates banditStashCardPick into a bare cardId without a meaningful action', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', { kind: 'banditStashCardPick', playerSeat: 0, cardId: 'card-4' });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'EndTurn', cardId: 'card-4' });
  });

  it('submitCommand translates recyclePick into recycleReplacement without a meaningful action', async () => {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedBody = await request.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', {
      kind: 'recyclePick',
      playerSeat: 0,
      recycleReplacement: 'Bandit',
    });
    expect(capturedBody).toEqual({ playerSeat: 0, action: 'EndTurn', recycleReplacement: 'Bandit' });
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

    const response = await gamesApi.submitCommand('game-1', { kind: 'action', playerSeat: 0, action: 'EndTurn' });
    expect(response.succeeded).toBe(false);
    expect(response.errorMessage).toBe('Action is not allowed right now.');
  });

  it('getResult fetches the final scoreboard', async () => {
    const result = await gamesApi.getResult('game-1');
    expect(result.winningPlayerIndex).toBe(0);
    expect(result.scoreLines).toHaveLength(1);
  });
});
