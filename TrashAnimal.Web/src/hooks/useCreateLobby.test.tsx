import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { useCreateLobby } from './useCreateLobby';
import { queryKeys } from '../api/queryKeys';

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return {
    queryClient,
    wrapper: ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    ),
  };
}

describe('useCreateLobby', () => {
  it('creates a lobby and seeds the lobby query cache with the response', async () => {
    const { queryClient, wrapper } = createWrapper();
    const { result } = renderHook(() => useCreateLobby(), { wrapper });

    result.current.mutate({ nickname: 'Alice' });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    const lobbyId = result.current.data?.lobby.lobbyId;
    expect(lobbyId).toBeDefined();
    expect(queryClient.getQueryData(queryKeys.lobby(lobbyId!))).toEqual(result.current.data?.lobby);
  });
});
