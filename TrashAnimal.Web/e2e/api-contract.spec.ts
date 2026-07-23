import { test, expect } from '@playwright/test';

/**
 * API Contract Tests — Playwright E2E integration tests
 *
 * Same contract validations as contracts.test.ts but running in real browsers
 * (Chromium, Firefox, WebKit). Uses Playwright's request API to make HTTP calls
 * against the real TrashAnimal.Api backend.
 *
 * These tests verify:
 * - Response shapes match TypeScript types
 * - Polymorphic union structure (GameCommandRequest with "kind" discriminator)
 * - Enum serialization (strings, not numeric enums)
 * - Field naming (camelCase, not PascalCase)
 * - HTTP status codes
 *
 * Prerequisites: TrashAnimal.Api running at http://localhost:5080
 *
 * Run: npm run test:e2e e2e/api-contract.spec.ts
 */

const API_BASE_URL = 'http://localhost:5080';

test.describe('API Contract Tests (Playwright)', () => {
  let gameId: string;

  test.beforeAll(async ({ playwright }) => {
    // Create a game via direct HTTP for use in other tests
    const browser = await playwright.chromium.launch();
    const context = await browser.createBrowserContext();
    const page = await context.newPage();

    const response = await page.request.post(`${API_BASE_URL}/games`, {
      data: {
        playerNames: ['Alice', 'Bob'],
      },
    });

    expect(response.status()).toBe(201);
    const data = await response.json();
    gameId = data.gameId;

    await context.close();
    await browser.close();
  });

  test.describe('Polymorphic Union Structure', () => {
    test('accepts PlayActionCommand with kind: "action"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'action',
          playerSeat: 0,
          action: 'RollDie',
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('accepts PlayFeeshCommand with kind: "playFeesh"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'playFeesh',
          playerSeat: 0,
          cardId: '00000000-0000-0000-0000-000000000000',
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('accepts PlayShinyCommand with kind: "playShiny"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'playShiny',
          playerSeat: 0,
          victimSeat: 1,
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('accepts ResolveTokenStealCommand with kind: "resolveTokenSteal"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'resolveTokenSteal',
          playerSeat: 0,
          victimSeat: 1,
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('accepts CardPickCommand with kind: "cardPick"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'cardPick',
          playerSeat: 0,
          cardId: '00000000-0000-0000-0000-000000000001',
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('accepts DoubleStashCommand with kind: "doubleStash"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'doubleStash',
          playerSeat: 0,
          cardIds: ['00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000003'],
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('accepts RecyclePickCommand with kind: "recyclePick"', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'recyclePick',
          playerSeat: 0,
          replacement: 'Bandit',
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });
  });

  test.describe('Response Shapes', () => {
    test('POST /games returns GameCreationResponse', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games`, {
        data: {
          playerNames: ['Test1', 'Test2'],
        },
      });

      expect(response.status()).toBe(201);

      const body = await response.json();
      expect(body).toHaveProperty('gameId');
      expect(body).toHaveProperty('view');
      expect(body).toHaveProperty('allowedActions');

      assertGameViewShape(body.view);
    });

    test('GET /games/{id}/view returns PlayerViewResponse', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      expect(response.status()).toBe(200);

      const body = await response.json();
      expect(body).toHaveProperty('view');
      expect(body).toHaveProperty('allowedActions');
      expect(body).toHaveProperty('revision');

      assertGameViewShape(body.view);
    });

    test('POST /games/{id}/commands returns GameCommandResponse', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'action',
          playerSeat: 0,
          action: 'RollDie',
        },
      });

      expect([200, 422]).toContain(response.status());

      const body = await response.json();
      assertGameCommandResponseShape(body);
    });

    test('GET /games/{id}/result returns GameResultResponse (or error)', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/result`);

      expect([200, 404, 422]).toContain(response.status());

      if (response.status() === 200) {
        const body = await response.json();
        expect(body).toHaveProperty('scoreLines');
        expect(body).toHaveProperty('winningPlayerIndex');
        expect(Array.isArray(body.scoreLines)).toBe(true);
      }
    });
  });

  test.describe('Enum Serialization', () => {
    test('GameState is a string (not numeric)', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      const body = await response.json();
      const state = body.view.state;

      expect(typeof state).toBe('string');
      expect(isNaN(Number(state))).toBe(true);

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

    test('GameAction[] items are strings (not numeric)', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      const body = await response.json();
      const actions = body.view.allowedActions || [];

      expect(Array.isArray(actions)).toBe(true);
      actions.forEach((action: unknown) => {
        expect(typeof action).toBe('string');
        expect(isNaN(Number(action as string))).toBe(true);
      });
    });

    test('CardName values are strings (not numeric)', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      const body = await response.json();
      const cardNames = body.view.handCardNames || [];

      expect(Array.isArray(cardNames)).toBe(true);
      cardNames.forEach((cardName: unknown) => {
        expect(typeof cardName).toBe('string');
        expect(isNaN(Number(cardName as string))).toBe(true);
      });
    });

    test('TokenAction values are strings (not numeric)', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

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

  test.describe('Field Naming Convention', () => {
    test('response fields use camelCase (not PascalCase)', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games`, {
        data: {
          playerNames: ['A', 'B'],
        },
      });

      const body = await response.json();

      expect(body).toHaveProperty('gameId');
      expect(body).not.toHaveProperty('GameId');

      expect(body).toHaveProperty('allowedActions');
      expect(body).not.toHaveProperty('AllowedActions');

      const view = body.view;
      expect(view).toHaveProperty('currentPlayerIndex');
      expect(view).not.toHaveProperty('CurrentPlayerIndex');
    });
  });

  test.describe('Nullable Fields', () => {
    test('GameView nullable fields handle null correctly', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      const body = await response.json();
      const view = body.view;

      expect(view).toHaveProperty('stealPhase');
      expect(view.stealPhase === null || typeof view.stealPhase === 'object').toBe(true);

      expect(view).toHaveProperty('tokenPhase');
      expect(view.tokenPhase === null || typeof view.tokenPhase === 'object').toBe(true);

      expect(view).toHaveProperty('yumYumResponderIndex');
      expect(view.yumYumResponderIndex === null || typeof view.yumYumResponderIndex === 'number').toBe(true);
    });
  });

  test.describe('HTTP Status Codes', () => {
    test('POST /games returns 201', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games`, {
        data: {
          playerNames: ['X', 'Y'],
        },
      });

      expect(response.status()).toBe(201);
    });

    test('GET /games/{id}/view returns 200', async ({ request }) => {
      const response = await request.get(`${API_BASE_URL}/games/${gameId}/view?playerSeat=0`);

      expect(response.status()).toBe(200);
    });

    test('POST /games/{id}/commands returns 200 or 422', async ({ request }) => {
      const response = await request.post(`${API_BASE_URL}/games/${gameId}/commands`, {
        data: {
          kind: 'action',
          playerSeat: 0,
          action: 'RollDie',
        },
      });

      expect([200, 422]).toContain(response.status());
    });
  });
});

/**
 * Helper: Validate GameView shape
 */
function assertGameViewShape(view: unknown): void {
  expect(view).toBeTruthy();
  expect(typeof view).toBe('object');

  const v = view as Record<string, unknown>;

  expect(v).toHaveProperty('state');
  expect(typeof v.state).toBe('string');

  expect(v).toHaveProperty('currentPlayerIndex');
  expect(typeof v.currentPlayerIndex).toBe('number');

  expect(v).toHaveProperty('handCardNames');
  expect(Array.isArray(v.handCardNames)).toBe(true);

  expect(v).toHaveProperty('phaseOneTokens');
  expect(Array.isArray(v.phaseOneTokens)).toBe(true);
}

/**
 * Helper: Validate GameCommandResponse shape
 */
function assertGameCommandResponseShape(response: unknown): void {
  expect(response).toBeTruthy();
  expect(typeof response).toBe('object');

  const r = response as Record<string, unknown>;

  expect(r).toHaveProperty('succeeded');
  expect(typeof r.succeeded).toBe('boolean');

  expect(r).toHaveProperty('errorMessage');
  expect(r.errorMessage === null || typeof r.errorMessage === 'string').toBe(true);

  expect(r).toHaveProperty('view');
  expect(r).toHaveProperty('allowedActions');

  if (r.succeeded) {
    expect(r.view).not.toBeNull();
    assertGameViewShape(r.view);
    expect(Array.isArray(r.allowedActions)).toBe(true);
  } else {
    expect(r.view).toBeNull();
    expect(r.allowedActions).toBeNull();
  }
}
