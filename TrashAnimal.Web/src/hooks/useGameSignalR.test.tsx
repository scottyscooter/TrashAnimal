import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { queryKeys } from '../api/queryKeys';

// Statically importing these mocks (rather than vi.mock's factory closing over outer consts) would
// hit hoisting order issues, so — same pattern as api/signalRClient.test.ts — declare the mocks
// first and dynamically import the module under test afterward.
const connectToGameHub = vi.fn();
vi.mock('../api/signalRClient', () => ({
  connectToGameHub: (...args: unknown[]) => connectToGameHub(...args),
}));

const getView = vi.fn();
vi.mock('../api/gamesApi', () => ({
  gamesApi: { getView: (...args: unknown[]) => getView(...args) },
}));

const { useGameSignalR } = await import('./useGameSignalR');

function createWrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useGameSignalR', () => {
  let queryClient: QueryClient;
  let subscriptionStop: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    subscriptionStop = vi.fn().mockResolvedValue(undefined);
    connectToGameHub.mockResolvedValue({ connection: {}, stop: subscriptionStop });
  });

  it('updates the cache on reconnect when the fresh revision differs from the cached one', async () => {
    const queryKey = queryKeys.gameView('game-1', 0);
    queryClient.setQueryData(queryKey, { view: {}, allowedActions: [], revision: 1 });
    getView.mockResolvedValue({ view: {}, allowedActions: [], revision: 2 });

    renderHook(() => useGameSignalR('game-1', 0), { wrapper: createWrapper(queryClient) });
    await waitFor(() => expect(connectToGameHub).toHaveBeenCalled());
    const handlers = connectToGameHub.mock.calls[0][1];

    await handlers.onReconnected();

    expect(queryClient.getQueryData(queryKey)).toEqual({ view: {}, allowedActions: [], revision: 2 });
  });

  it('does not touch the cache on reconnect when the fresh revision matches the cached one', async () => {
    const queryKey = queryKeys.gameView('game-1', 0);
    const cached = { view: {}, allowedActions: [], revision: 3 };
    queryClient.setQueryData(queryKey, cached);
    getView.mockResolvedValue({ view: {}, allowedActions: [], revision: 3 });

    renderHook(() => useGameSignalR('game-1', 0), { wrapper: createWrapper(queryClient) });
    await waitFor(() => expect(connectToGameHub).toHaveBeenCalled());
    const handlers = connectToGameHub.mock.calls[0][1];
    const setQueryDataSpy = vi.spyOn(queryClient, 'setQueryData');

    await handlers.onReconnected();

    expect(setQueryDataSpy).not.toHaveBeenCalled();
    expect(queryClient.getQueryData(queryKey)).toBe(cached);
  });

  it('sets the cache on reconnect when there is no cached data yet', async () => {
    const queryKey = queryKeys.gameView('game-1', 0);
    const fresh = { view: {}, allowedActions: [], revision: 1 };
    getView.mockResolvedValue(fresh);

    renderHook(() => useGameSignalR('game-1', 0), { wrapper: createWrapper(queryClient) });
    await waitFor(() => expect(connectToGameHub).toHaveBeenCalled());
    const handlers = connectToGameHub.mock.calls[0][1];

    await handlers.onReconnected();

    expect(queryClient.getQueryData(queryKey)).toEqual(fresh);
  });

  it('invalidates the game view query when GameUpdated fires', async () => {
    renderHook(() => useGameSignalR('game-1', 0), { wrapper: createWrapper(queryClient) });
    await waitFor(() => expect(connectToGameHub).toHaveBeenCalled());
    const handlers = connectToGameHub.mock.calls[0][1];
    const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');

    handlers.onGameUpdated();

    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: queryKeys.gameView('game-1', 0) });
  });

  it('stops the subscription on unmount', async () => {
    const { unmount } = renderHook(() => useGameSignalR('game-1', 0), {
      wrapper: createWrapper(queryClient),
    });
    await waitFor(() => expect(connectToGameHub).toHaveBeenCalled());

    unmount();

    await waitFor(() => expect(subscriptionStop).toHaveBeenCalled());
  });
});
