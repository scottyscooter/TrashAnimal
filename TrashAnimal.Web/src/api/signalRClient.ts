import * as signalR from '@microsoft/signalr';
import { API_BASE_URL } from './httpClient';
import type { GameUpdateEnvelope, LobbyUpdateEnvelope } from './types';

function createHubConnection(hubPath: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}${hubPath}`)
    .withAutomaticReconnect()
    .build();
}

export interface HubSubscription {
  connection: signalR.HubConnection;
  stop: () => Promise<void>;
}

interface CommonHubHandlers {
  /**
   * Called whenever a hub operation fails asynchronously with nowhere else to propagate to: the
   * initial connect/join, a reconnect's re-join, or `onReconnected` throwing. Centralized here
   * (rather than left to each caller) because every one of those failures used to be silently
   * `void`-d — the app would quietly fall back to no live updates with no signal anywhere. Falls
   * back to `console.error` if the caller doesn't supply one, so this can never regress to silent.
   */
  onConnectionError?: (error: unknown) => void;
}

function reportConnectionError(handlers: CommonHubHandlers, error: unknown): void {
  if (handlers.onConnectionError) {
    handlers.onConnectionError(error);
  } else {
    console.error('SignalR connection error:', error);
  }
}

export interface GameHubHandlers extends CommonHubHandlers {
  onGameUpdated: (envelope: GameUpdateEnvelope) => void;
  /**
   * Called after signalr's automatic reconnect completes. GameHub reconnect should compare the
   * cached PlayerViewResponse.Revision against a freshly-fetched view's revision and only update
   * state if they differ, to detect updates missed while disconnected (review note 3 / GameHub
   * doc comments) — implement that comparison in the caller (see useGameSignalR).
   */
  onReconnected?: () => void | Promise<void>;
}

/**
 * Connects to GameHub, joins the game's group, and registers the GameUpdated handler.
 * Rejects if the initial connect or join fails (reported via `onConnectionError` first).
 */
export async function connectToGameHub(gameId: string, handlers: GameHubHandlers): Promise<HubSubscription> {
  const connection = createHubConnection('/hubs/game');
  connection.on('GameUpdated', (envelope: GameUpdateEnvelope) => handlers.onGameUpdated(envelope));
  connection.onreconnected(() => {
    connection.invoke('JoinGameAsync', gameId).catch((error) => reportConnectionError(handlers, error));
    Promise.resolve(handlers.onReconnected?.()).catch((error) => reportConnectionError(handlers, error));
  });

  try {
    await connection.start();
    await connection.invoke('JoinGameAsync', gameId);
  } catch (error) {
    reportConnectionError(handlers, error);
    throw error;
  }

  return {
    connection,
    stop: async () => {
      await connection.invoke('LeaveGameAsync', gameId).catch(() => {});
      await connection.stop();
    },
  };
}

export interface LobbyHubHandlers extends CommonHubHandlers {
  onLobbyUpdated: (envelope: LobbyUpdateEnvelope) => void;
  /**
   * LobbyHub reconnect always refetches rather than comparing a cached revision — LobbyView has
   * no Revision field, and lobby state is small/low-frequency enough that unconditional refetch on
   * reconnect is an accepted, documented asymmetry with GameHub (review note 3).
   */
  onReconnected?: () => void | Promise<void>;
}

/**
 * Connects to LobbyHub, joins the lobby's group, and registers the LobbyUpdated handler.
 * Rejects if the initial connect or join fails (reported via `onConnectionError` first).
 */
export async function connectToLobbyHub(lobbyId: string, handlers: LobbyHubHandlers): Promise<HubSubscription> {
  const connection = createHubConnection('/hubs/lobby');
  connection.on('LobbyUpdated', (envelope: LobbyUpdateEnvelope) => handlers.onLobbyUpdated(envelope));
  connection.onreconnected(() => {
    connection.invoke('JoinLobbyAsync', lobbyId).catch((error) => reportConnectionError(handlers, error));
    Promise.resolve(handlers.onReconnected?.()).catch((error) => reportConnectionError(handlers, error));
  });

  try {
    await connection.start();
    await connection.invoke('JoinLobbyAsync', lobbyId);
  } catch (error) {
    reportConnectionError(handlers, error);
    throw error;
  }

  return {
    connection,
    stop: async () => {
      await connection.invoke('LeaveLobbyAsync', lobbyId).catch(() => {});
      await connection.stop();
    },
  };
}
