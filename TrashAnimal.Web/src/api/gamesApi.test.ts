import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { API_BASE_URL } from './httpClient';
import { gamesApi } from './gamesApi';
import type { SubmitCommandRequest } from './types';

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

/**
 * TrashAnimal.Api's POST /games/{gameId}/commands now binds a `GameCommandRequest` polymorphic
 * union keyed by a `kind` discriminator (System.Text.Json [JsonPolymorphic]/[JsonDerivedType]),
 * with each subtype carrying only its own fields (PlayFeeshCommand is just {playerSeat, cardId},
 * for example — there is no `action` filler field on the wire anymore, and card-pick variants
 * collapse to a single `cardPick` kind since CardPickCommand is routed server-side by ambient
 * GameState/TokenPhaseStep). `gamesApi.ts`'s `toWireRequest` was not updated for this change and
 * still emits the old flat {playerSeat, action, cardId?, ...} shape — these tests document the
 * wire shape the backend actually expects and are expected to fail until toWireRequest is rewritten.
 */
describe('gamesApi submitCommand wire contract (GameCommandRequest polymorphic union)', () => {
  async function captureWireBody(request: SubmitCommandRequest): Promise<unknown> {
    let capturedBody: unknown;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request: httpRequest }) => {
        capturedBody = await httpRequest.json();
        return HttpResponse.json({ succeeded: true, errorMessage: null, view: null, allowedActions: [] });
      }),
    );

    await gamesApi.submitCommand('game-1', request);
    return capturedBody;
  }

  it('sends kind "action" matching PlayActionCommand for plain actions', async () => {
    const body = await captureWireBody({ kind: 'action', playerSeat: 0, action: 'RollDie' });
    expect(body).toEqual({ kind: 'action', playerSeat: 0, action: 'RollDie' });
  });

  it('sends kind "playFeesh" with only playerSeat + cardId, matching PlayFeeshCommand', async () => {
    const body = await captureWireBody({ kind: 'playFeesh', playerSeat: 1, cardId: 'card-1' });
    expect(body).toEqual({ kind: 'playFeesh', playerSeat: 1, cardId: 'card-1' });
  });

  it('sends kind "playShiny" with only playerSeat + victimSeat, matching PlayShinyCommand', async () => {
    const body = await captureWireBody({ kind: 'playShiny', playerSeat: 0, victimSeat: 2 });
    expect(body).toEqual({ kind: 'playShiny', playerSeat: 0, victimSeat: 2 });
  });

  it('sends kind "resolveTokenSteal" with only playerSeat + victimSeat, matching ResolveTokenStealCommand', async () => {
    const body = await captureWireBody({ kind: 'resolveTokenSteal', playerSeat: 1, victimSeat: 3 });
    expect(body).toEqual({ kind: 'resolveTokenSteal', playerSeat: 1, victimSeat: 3 });
  });

  it('sends kind "cardPick" for stealCardPick, matching CardPickCommand', async () => {
    const body = await captureWireBody({ kind: 'stealCardPick', playerSeat: 0, cardId: 'card-2' });
    expect(body).toEqual({ kind: 'cardPick', playerSeat: 0, cardId: 'card-2' });
  });

  it('sends kind "cardPick" for stashTrashCardPick, matching CardPickCommand', async () => {
    const body = await captureWireBody({ kind: 'stashTrashCardPick', playerSeat: 0, cardId: 'card-3' });
    expect(body).toEqual({ kind: 'cardPick', playerSeat: 0, cardId: 'card-3' });
  });

  it('sends kind "cardPick" for banditStashCardPick, matching CardPickCommand', async () => {
    const body = await captureWireBody({ kind: 'banditStashCardPick', playerSeat: 0, cardId: 'card-4' });
    expect(body).toEqual({ kind: 'cardPick', playerSeat: 0, cardId: 'card-4' });
  });

  it('sends kind "doubleStash" with cardIds, matching DoubleStashCommand', async () => {
    const body = await captureWireBody({
      kind: 'doubleStashSubmit',
      playerSeat: 0,
      cardIds: ['card-a', 'card-b'],
    });
    expect(body).toEqual({ kind: 'doubleStash', playerSeat: 0, cardIds: ['card-a', 'card-b'] });
  });

  it('sends kind "recyclePick" with a replacement field, matching RecyclePickCommand', async () => {
    const body = await captureWireBody({
      kind: 'recyclePick',
      playerSeat: 0,
      recycleReplacement: 'Bandit',
    });
    expect(body).toEqual({ kind: 'recyclePick', playerSeat: 0, replacement: 'Bandit' });
  });
});
