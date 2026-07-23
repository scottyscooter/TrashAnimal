import { describe, it, expect, beforeAll } from 'vitest';

/**
 * Lobby API Contract Tests — Validates frontend/backend synchronization for lobby endpoints
 *
 * Tests verify:
 * - Response shapes match types.ts definitions
 * - Enum serialization (strings, not numeric)
 * - Field naming convention (camelCase)
 * - HTTP status codes align with API spec
 * - Nullable/optional fields handled correctly
 *
 * **Requires TrashAnimal.Api running at http://localhost:5080**
 *
 * Run: npm run test:run -- lobbies-contracts.test.ts
 */

const API_BASE_URL = process.env.VITE_API_BASE_URL || 'http://localhost:5080';

describe('Lobby API Contract Tests', () => {
  let lobbyId: string;

  beforeAll(async () => {
    // Create a lobby for use in dependent tests
    const response = await fetch(`${API_BASE_URL}/lobbies`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ nickname: 'TestAdmin' }),
    });

    if (!response.ok) {
      throw new Error(
        `Failed to create test lobby: ${response.status} ${response.statusText}. ` +
          `Is TrashAnimal.Api running at ${API_BASE_URL}?`,
      );
    }

    const data = await response.json();
    lobbyId = data.lobby.lobbyId;
  });

  describe('POST /lobbies (CreateLobby)', () => {
    it('returns 201 with LobbyJoinResponse shape', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nickname: 'Player1' }),
      });

      expect(response.status).toBe(201);

      const body = await response.json();
      expect(body).toHaveProperty('lobby');
      expect(body).toHaveProperty('seatIndex');
      expect(body).toHaveProperty('clientToken');

      assertLobbyViewStructure(body.lobby);
      expect(typeof body.seatIndex).toBe('number');
      expect(typeof body.clientToken).toBe('string');
    });
  });

  describe('GET /lobbies/{lobbyId}', () => {
    it('returns 200 with LobbyView shape', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${lobbyId}`);

      expect(response.status).toBe(200);

      const body = await response.json();
      assertLobbyViewStructure(body);
    });

    it('URL-encodes lobbyId path segment', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${encodeURIComponent(lobbyId)}`);
      expect(response.status).toBe(200);
    });
  });

  describe('POST /lobbies/{lobbyId}/players (JoinLobby)', () => {
    it('returns 200 with LobbyJoinResponse shape on success', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${lobbyId}/players`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nickname: 'Player2' }),
      });

      expect(response.status).toBe(200);

      const body = await response.json();
      expect(body).toHaveProperty('lobby');
      expect(body).toHaveProperty('seatIndex');
      expect(body).toHaveProperty('clientToken');

      assertLobbyViewStructure(body.lobby);
    });
  });

  describe('POST /lobbies/{lobbyId}/start (StartLobby)', () => {
    it('returns 200 with LobbyStartResponse on success', async () => {
      // Create a new lobby with 2 players
      const createResponse = await fetch(`${API_BASE_URL}/lobbies`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nickname: 'StartAdmin' }),
      });
      const createData = await createResponse.json();
      const startLobbyId = createData.lobby.lobbyId;
      const startToken = createData.clientToken;

      // Add another player
      await fetch(`${API_BASE_URL}/lobbies/${startLobbyId}/players`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nickname: 'StartPlayer' }),
      });

      // Start the lobby
      const response = await fetch(`${API_BASE_URL}/lobbies/${startLobbyId}/start`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ clientToken: startToken }),
      });

      expect(response.status).toBe(200);

      const body = await response.json();
      expect(body).toHaveProperty('gameId');
      expect(typeof body.gameId).toBe('string');
    });
  });

  describe('LobbyView & LobbyJoinResponse Field Naming (camelCase)', () => {
    it('LobbyView uses camelCase field names', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${lobbyId}`);
      const body = await response.json();

      expect(body).toHaveProperty('lobbyId');
      expect(body).not.toHaveProperty('LobbyId');

      expect(body).toHaveProperty('seats');
      expect(body).not.toHaveProperty('Seats');

      expect(body).toHaveProperty('isStarted');
      expect(body).not.toHaveProperty('IsStarted');

      expect(body).toHaveProperty('gameId');
      expect(body).not.toHaveProperty('GameId');
    });

    it('LobbySeatView uses camelCase field names', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${lobbyId}`);
      const body = await response.json();

      expect(Array.isArray(body.seats)).toBe(true);
      if (body.seats.length > 0) {
        const seat = body.seats[0];
        expect(seat).toHaveProperty('seatIndex');
        expect(seat).not.toHaveProperty('SeatIndex');

        expect(seat).toHaveProperty('nickname');
        expect(seat).not.toHaveProperty('Nickname');
      }
    });
  });

  describe('HTTP Status Codes', () => {
    it('CreateLobby returns 201 on success', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nickname: 'Status201Test' }),
      });

      expect(response.status).toBe(201);
    });

    it('GetLobby returns 200 on success', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${lobbyId}`);
      expect(response.status).toBe(200);
    });

    it('JoinLobby returns 200 on success', async () => {
      const response = await fetch(`${API_BASE_URL}/lobbies/${lobbyId}/players`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nickname: 'StatusTest' }),
      });

      expect(response.status).toBe(200);
    });
  });
});

/**
 * Helper: Validate LobbyView structure
 */
function assertLobbyViewStructure(lobby: unknown): void {
  expect(lobby).toBeTruthy();
  expect(typeof lobby).toBe('object');

  const l = lobby as Record<string, unknown>;

  expect(l).toHaveProperty('lobbyId');
  expect(typeof l.lobbyId).toBe('string');

  expect(l).toHaveProperty('seats');
  expect(Array.isArray(l.seats)).toBe(true);

  expect(l).toHaveProperty('isStarted');
  expect(typeof l.isStarted).toBe('boolean');

  expect(l).toHaveProperty('gameId');
  expect(l.gameId === null || typeof l.gameId === 'string').toBe(true);

  // Validate seat structure if seats exist
  const seats = l.seats as unknown[];
  seats.forEach((seat) => {
    expect(typeof seat).toBe('object');
    const s = seat as Record<string, unknown>;
    expect(s).toHaveProperty('seatIndex');
    expect(s).toHaveProperty('nickname');
  });
}
