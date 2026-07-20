import { useMutation, useQueryClient } from '@tanstack/react-query';
import { lobbiesApi } from '../api/lobbiesApi';
import { queryKeys } from '../api/queryKeys';
import type { StartLobbyRequest } from '../api/types';

export function useStartLobby(lobbyId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: StartLobbyRequest) => lobbiesApi.startLobby(lobbyId, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.lobby(lobbyId) });
    },
  });
}
