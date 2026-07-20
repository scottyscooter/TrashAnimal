import { describe, expect, it } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '../test/msw/server';
import { API_BASE_URL } from './httpClient';
import { gamesApi } from './gamesApi';

describe('gamesApi', () => {
  it('createGame posts playerNames and returns the created game', async () => {
    const response = await gamesApi.createGame({ playerNames: ['Alice', 'Bob'] });
    expect(response.gameId).toBe('22222222-2222-2222-2222-222222222222');
    expect(response.view.state).toBe('RollPhase');
  });

  it('getView fetches the per-seat view', async () => {
    const response = await gamesApi.getView('22222222-2222-2222-2222-222222222222', 0);
    expect(response.revision).toBe(1);
    expect(response.view.handCardNames).toEqual(['Shiny', 'Feesh']);
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
