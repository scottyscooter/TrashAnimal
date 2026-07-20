import { beforeEach, describe, expect, it, vi } from 'vitest';

// msw intercepts fetch/XHR, not SignalR's transport negotiation, so reconnect/re-join logic is
// covered here via vi.mock('@microsoft/signalr') instead (review note 6). The real reconnect path
// is backstopped by Task 8's Playwright e2e suite against a live API.
const connection = {
  on: vi.fn(),
  onreconnected: vi.fn(),
  start: vi.fn().mockResolvedValue(undefined),
  invoke: vi.fn().mockResolvedValue(undefined),
  stop: vi.fn().mockResolvedValue(undefined),
};

const builder = {
  withUrl: vi.fn(),
  withAutomaticReconnect: vi.fn(),
  build: vi.fn(),
};
builder.withUrl.mockReturnValue(builder);
builder.withAutomaticReconnect.mockReturnValue(builder);
builder.build.mockReturnValue(connection);

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(() => builder),
}));

const { connectToGameHub, connectToLobbyHub } = await import('./signalRClient');

describe('signalRClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('connectToGameHub starts the connection, joins the game group, and registers GameUpdated', async () => {
    const onGameUpdated = vi.fn();
    await connectToGameHub('game-1', { onGameUpdated });

    expect(connection.start).toHaveBeenCalledOnce();
    expect(connection.invoke).toHaveBeenCalledWith('JoinGameAsync', 'game-1');

    const [, handler] = connection.on.mock.calls.find(([event]) => event === 'GameUpdated')!;
    const envelope = { gameId: 'game-1', revision: 2, actingPlayerSeat: 0, currentGameState: 'RollPhase' };
    handler(envelope);
    expect(onGameUpdated).toHaveBeenCalledWith(envelope);
  });

  it('connectToGameHub re-joins the group and calls onReconnected after signalr reconnects', async () => {
    const onReconnected = vi.fn();
    await connectToGameHub('game-1', { onGameUpdated: vi.fn(), onReconnected });

    const reconnectHandler = connection.onreconnected.mock.calls[0][0];
    reconnectHandler();

    expect(connection.invoke).toHaveBeenCalledWith('JoinGameAsync', 'game-1');
    expect(onReconnected).toHaveBeenCalledOnce();
  });

  it('stop leaves the game group before stopping the connection', async () => {
    const subscription = await connectToGameHub('game-1', { onGameUpdated: vi.fn() });
    await subscription.stop();

    expect(connection.invoke).toHaveBeenCalledWith('LeaveGameAsync', 'game-1');
    expect(connection.stop).toHaveBeenCalledOnce();
  });

  it('connectToLobbyHub joins the lobby group and registers LobbyUpdated', async () => {
    const onLobbyUpdated = vi.fn();
    await connectToLobbyHub('lobby-1', { onLobbyUpdated });

    expect(connection.invoke).toHaveBeenCalledWith('JoinLobbyAsync', 'lobby-1');
    const [, handler] = connection.on.mock.calls.find(([event]) => event === 'LobbyUpdated')!;
    const envelope = { lobbyId: 'lobby-1', revision: 1, lobby: { lobbyId: 'lobby-1', seats: [], isStarted: false, gameId: null } };
    handler(envelope);
    expect(onLobbyUpdated).toHaveBeenCalledWith(envelope);
  });

  it('connectToLobbyHub always calls onReconnected (no revision to compare) on reconnect', async () => {
    const onReconnected = vi.fn();
    await connectToLobbyHub('lobby-1', { onLobbyUpdated: vi.fn(), onReconnected });

    const reconnectHandler = connection.onreconnected.mock.calls[0][0];
    reconnectHandler();

    expect(connection.invoke).toHaveBeenCalledWith('JoinLobbyAsync', 'lobby-1');
    expect(onReconnected).toHaveBeenCalledOnce();
  });

  it('stop leaves the lobby group before stopping the connection', async () => {
    const subscription = await connectToLobbyHub('lobby-1', { onLobbyUpdated: vi.fn() });
    await subscription.stop();

    expect(connection.invoke).toHaveBeenCalledWith('LeaveLobbyAsync', 'lobby-1');
    expect(connection.stop).toHaveBeenCalledOnce();
  });

  it('connectToGameHub reports and rethrows when the initial connect fails', async () => {
    const error = new Error('negotiate failed');
    connection.start.mockRejectedValueOnce(error);
    const onConnectionError = vi.fn();

    await expect(
      connectToGameHub('game-1', { onGameUpdated: vi.fn(), onConnectionError }),
    ).rejects.toBe(error);
    expect(onConnectionError).toHaveBeenCalledWith(error);
  });

  it('connectToGameHub falls back to console.error when onConnectionError is not provided', async () => {
    const error = new Error('negotiate failed');
    connection.start.mockRejectedValueOnce(error);
    const consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    await expect(connectToGameHub('game-1', { onGameUpdated: vi.fn() })).rejects.toBe(error);
    expect(consoleErrorSpy).toHaveBeenCalledWith('SignalR connection error:', error);

    consoleErrorSpy.mockRestore();
  });

  it('connectToGameHub reports a failed re-join after reconnect via onConnectionError', async () => {
    const onConnectionError = vi.fn();
    await connectToGameHub('game-1', { onGameUpdated: vi.fn(), onConnectionError });

    const error = new Error('rejoin failed');
    connection.invoke.mockRejectedValueOnce(error);
    const reconnectHandler = connection.onreconnected.mock.calls[0][0];
    reconnectHandler();

    await vi.waitFor(() => expect(onConnectionError).toHaveBeenCalledWith(error));
  });

  it('connectToGameHub reports a throwing onReconnected via onConnectionError', async () => {
    const onConnectionError = vi.fn();
    const error = new Error('reconnect handler failed');
    await connectToGameHub('game-1', {
      onGameUpdated: vi.fn(),
      onReconnected: () => Promise.reject(error),
      onConnectionError,
    });

    const reconnectHandler = connection.onreconnected.mock.calls[0][0];
    reconnectHandler();

    await vi.waitFor(() => expect(onConnectionError).toHaveBeenCalledWith(error));
  });

  it('connectToLobbyHub reports and rethrows when the initial connect fails', async () => {
    const error = new Error('negotiate failed');
    connection.start.mockRejectedValueOnce(error);
    const onConnectionError = vi.fn();

    await expect(
      connectToLobbyHub('lobby-1', { onLobbyUpdated: vi.fn(), onConnectionError }),
    ).rejects.toBe(error);
    expect(onConnectionError).toHaveBeenCalledWith(error);
  });
});
