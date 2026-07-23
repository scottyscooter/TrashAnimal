import { describe, it, expect, beforeAll } from 'vitest';

/**
 * API Contract Tests — Node.js integration tests against real TrashAnimal.Api
 *
 * These tests verify that frontend/backend API contracts are synchronized by making
 * real HTTP calls to the backend and validating response shapes. They catch:
 *
 * 1. Response shape mismatches (missing/unexpected fields)
 * 2. Enum serialization issues (strings vs numeric enums)
 * 3. Field naming convention violations (camelCase vs PascalCase)
 * 4. Null/optional field handling bugs
 * 5. Request format mismatches (especially polymorphic union structure)
 *
 * **IMPORTANT:** Requires TrashAnimal.Api running at http://localhost:5080
 * Override via VITE_API_BASE_URL environment variable.
 *
 * Run: npm run test:run -- contracts.test.ts (or npm run test -- contracts.test.ts)
 */

const API_BASE_URL = process.env.VITE_API_BASE_URL || 'http://localhost:5080';

describe('API Contract Tests', () => {
  let gameId: string;

  beforeAll(async () => {
    // Create a game for use in dependent tests
    const response = await fetch(`${API_BASE_URL}/games`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerNames: ['Alice', 'Bob'] }),
    });

    if (!response.ok) {
      throw new Error(
        `Failed to create test game: ${response.status} ${response.statusText}. ` +
          `Is TrashAnimal.Api running at ${API_BASE_URL}?`,
      );
    }

    const data = await response.json();
    gameId = data.gameId;
  });

  describe('POST /games (CreateGame)', () => {
    it('returns 201 with GameCreationResponse shape', async () => {
      const response = await fetch(`${API_BASE_URL}/games`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerNames: ['Player1', 'Player2'] }),
      });

      expect(response.status).toBe(201);

      const body = await response.json();
      expect(body).toHaveProperty('gameId');
      expect(typeof body.gameId).toBe('string');

      expect(body).toHaveProperty('view');
      assertGameViewStructure(body.view);

      expect(body).toHaveProperty('allowedActions');
      expect(Array.isArray(body.allowedActions)).toBe(true);
    });
  });

  describe('GET /games/{gameId}/view', () => {
    it('returns 200 with PlayerViewResponse shape', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      expect(response.status).toBe(200);

      const body = await response.json();
      expect(body).toHaveProperty('view');
      expect(body).toHaveProperty('allowedActions');
      expect(body).toHaveProperty('revision');
      expect(typeof body.revision).toBe('number');

      assertGameViewStructure(body.view);
    });
  });

  describe('POST /games/{gameId}/commands — Polymorphic Union Structure', () => {
    it('accepts PlayActionCommand (kind: "action") with correct shape', async () => {
      const request = {
        kind: 'action',
        playerSeat: 0,
        action: 'RollDie',
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });

    it('accepts PlayFeeshCommand (kind: "playFeesh") with cardId', async () => {
      const request = {
        kind: 'playFeesh',
        playerSeat: 0,
        cardId: '00000000-0000-0000-0000-000000000000',
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });

    it('accepts PlayShinyCommand (kind: "playShiny") with victimSeat', async () => {
      const request = {
        kind: 'playShiny',
        playerSeat: 0,
        victimSeat: 1,
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });

    it('accepts ResolveTokenStealCommand (kind: "resolveTokenSteal") with victimSeat', async () => {
      const request = {
        kind: 'resolveTokenSteal',
        playerSeat: 0,
        victimSeat: 1,
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });

    it('accepts CardPickCommand (kind: "cardPick") with cardId', async () => {
      const request = {
        kind: 'cardPick',
        playerSeat: 0,
        cardId: '00000000-0000-0000-0000-000000000001',
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });

    it('accepts DoubleStashCommand (kind: "doubleStash") with cardIds array', async () => {
      const request = {
        kind: 'doubleStash',
        playerSeat: 0,
        cardIds: ['00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000003'],
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });

    it('accepts RecyclePickCommand (kind: "recyclePick") with replacement token', async () => {
      const request = {
        kind: 'recyclePick',
        playerSeat: 0,
        replacement: 'Bandit',
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);

      const body = await response.json();
      assertGameCommandResponseStructure(body);
    });
  });

  describe('GameCommandResponse shape', () => {
    it('returns GameCommandResponse with all required fields', async () => {
      const request = {
        kind: 'action',
        playerSeat: 0,
        action: 'RollDie',
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      const body = await response.json();

      expect(body).toHaveProperty('succeeded');
      expect(typeof body.succeeded).toBe('boolean');

      expect(body).toHaveProperty('errorMessage');
      expect(body.errorMessage === null || typeof body.errorMessage === 'string').toBe(true);

      expect(body).toHaveProperty('view');
      expect(body).toHaveProperty('allowedActions');

      // On success, view and allowedActions populated; on failure (422), both null
      if (body.succeeded) {
        expect(body.view).not.toBeNull();
        assertGameViewStructure(body.view);
        expect(Array.isArray(body.allowedActions)).toBe(true);
      } else {
        expect(body.view).toBeNull();
        expect(body.allowedActions).toBeNull();
      }
    });
  });

  describe('Enum Serialization (String, Not Numeric)', () => {
    it('GameState is a string (not numeric enum)', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);
      const body = await response.json();

      const state = body.view.state;
      expect(typeof state).toBe('string');
      expect(isNaN(Number(state))).toBe(true); // Should not be a number

      const validStates = [
        'RollPhase',
        'AwaitingYumYum',
        'AwaitingStealResponse',
        'AwaitingStealCardPick',
        'TokenPhase',
        'TurnEnd',
        'GameEnded',
      ];
      expect(validStates).toContain(state);
    });

    it('GameAction[] contains strings (not numeric enums)', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);
      const body = await response.json();

      const actions = body.view.allowedActions || [];
      expect(Array.isArray(actions)).toBe(true);

      actions.forEach((action: unknown) => {
        expect(typeof action).toBe('string');
        expect(isNaN(Number(action as string))).toBe(true);
      });
    });

    it('CardName values are strings (not numeric enums)', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);
      const body = await response.json();

      const cardNames = body.view.handCardNames || [];
      expect(Array.isArray(cardNames)).toBe(true);

      cardNames.forEach((cardName: unknown) => {
        expect(typeof cardName).toBe('string');
        expect(isNaN(Number(cardName as string))).toBe(true);
      });
    });

    it('TokenAction values are strings (not numeric enums)', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);
      const body = await response.json();

      const tokens = body.view.phaseOneTokens || [];
      expect(Array.isArray(tokens)).toBe(true);

      tokens.forEach((token: unknown) => {
        expect(typeof token).toBe('string');
        const validTokens = ['StashTrash', 'DoubleStash', 'DoubleTrash', 'Bandit', 'Steal', 'Recycle'];
        expect(validTokens).toContain(token);
      });
    });
  });

  describe('Field Naming Convention (camelCase)', () => {
    it('response fields use camelCase (not PascalCase)', async () => {
      const response = await fetch(`${API_BASE_URL}/games`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerNames: ['A', 'B'] }),
      });

      const body = await response.json();

      // Verify camelCase exists
      expect(body).toHaveProperty('gameId');
      expect(body).not.toHaveProperty('GameId');

      expect(body).toHaveProperty('allowedActions');
      expect(body).not.toHaveProperty('AllowedActions');

      // Verify nested fields in view
      const view = body.view;
      expect(view).toHaveProperty('currentPlayerIndex');
      expect(view).not.toHaveProperty('CurrentPlayerIndex');

      expect(view).toHaveProperty('handCardNames');
      expect(view).not.toHaveProperty('HandCardNames');
    });
  });

  describe('Nullable/Optional Fields', () => {
    it('handles nullable fields in GameView correctly', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);
      const body = await response.json();
      const view = body.view;

      // These fields exist and can be null or the expected type
      expect(view).toHaveProperty('stealPhase');
      expect(view.stealPhase === null || typeof view.stealPhase === 'object').toBe(true);

      expect(view).toHaveProperty('tokenPhase');
      expect(view.tokenPhase === null || typeof view.tokenPhase === 'object').toBe(true);

      expect(view).toHaveProperty('yumYumResponderIndex');
      expect(view.yumYumResponderIndex === null || typeof view.yumYumResponderIndex === 'number').toBe(true);

      expect(view).toHaveProperty('yumYumResponderName');
      expect(view.yumYumResponderName === null || typeof view.yumYumResponderName === 'string').toBe(true);
    });
  });

  describe('HTTP Status Codes', () => {
    it('POST /games returns 201', async () => {
      const response = await fetch(`${API_BASE_URL}/games`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ playerNames: ['X', 'Y'] }),
      });

      expect(response.status).toBe(201);
    });

    it('GET /games/{id}/view returns 200', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      expect(response.status).toBe(200);
    });

    it('POST /games/{id}/commands returns 200 or 422', async () => {
      const request = {
        kind: 'action',
        playerSeat: 0,
        action: 'RollDie',
      };

      const response = await fetch(`${API_BASE_URL}/games/${gameId}/commands`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      });

      expect([200, 422]).toContain(response.status);
    });

    it('GET /games/{id}/result returns 200, 404, or 422', async () => {
      const response = await fetch(`${API_BASE_URL}/games/${gameId}/result`);

      expect([200, 404, 422]).toContain(response.status);
    });
  });
});

/**
 * Helper: Validate GameView structure
 */
function assertGameViewStructure(view: unknown): void {
  expect(view).toBeTruthy();
  expect(typeof view).toBe('object');

  const v = view as Record<string, unknown>;

  // Required fields
  expect(v).toHaveProperty('state');
  expect(typeof v.state).toBe('string');

  expect(v).toHaveProperty('currentPlayerIndex');
  expect(typeof v.currentPlayerIndex).toBe('number');

  expect(v).toHaveProperty('currentPlayerName');
  expect(typeof v.currentPlayerName).toBe('string');

  expect(v).toHaveProperty('isBusted');
  expect(typeof v.isBusted).toBe('boolean');

  expect(v).toHaveProperty('forcedRollRemaining');
  expect(typeof v.forcedRollRemaining).toBe('boolean');

  expect(v).toHaveProperty('phaseOneTokens');
  expect(Array.isArray(v.phaseOneTokens)).toBe(true);

  expect(v).toHaveProperty('handCardNames');
  expect(Array.isArray(v.handCardNames)).toBe(true);

  // Nullable fields (must exist)
  expect(v).toHaveProperty('yumYumResponderIndex');
  expect(v).toHaveProperty('yumYumResponderName');
  expect(v).toHaveProperty('stealPhase');
  expect(v).toHaveProperty('tokenPhase');
}

/**
 * Helper: Validate GameCommandResponse structure
 */
function assertGameCommandResponseStructure(response: unknown): void {
  expect(response).toBeTruthy();
  expect(typeof response).toBe('object');

  const r = response as Record<string, unknown>;

  expect(r).toHaveProperty('succeeded');
  expect(typeof r.succeeded).toBe('boolean');

  expect(r).toHaveProperty('errorMessage');
  expect(r.errorMessage === null || typeof r.errorMessage === 'string').toBe(true);

  expect(r).toHaveProperty('view');
  expect(r).toHaveProperty('allowedActions');

  // On success, view and allowedActions should be populated; on failure, null
  if (r.succeeded) {
    expect(r.view).not.toBeNull();
    assertGameViewStructure(r.view);
    expect(Array.isArray(r.allowedActions)).toBe(true);
  } else {
    expect(r.view).toBeNull();
    expect(r.allowedActions).toBeNull();
  }
}
