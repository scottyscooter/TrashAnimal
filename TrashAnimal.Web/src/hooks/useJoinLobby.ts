import { useMutation, useQueryClient } from '@tanstack/react-query';
import { lobbiesApi } from '../api/lobbiesApi';
import { queryKeys } from '../api/queryKeys';
import type { JoinLobbyRequest } from '../api/types';

export function useJoinLobby(lobbyId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: JoinLobbyRequest) => lobbiesApi.joinLobby(lobbyId, request),
    onSuccess: (response) => {
      queryClient.setQueryData(queryKeys.lobby(lobbyId), response.lobby);
    },
  });
}
