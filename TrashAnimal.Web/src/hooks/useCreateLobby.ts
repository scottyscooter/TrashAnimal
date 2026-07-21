import { useMutation, useQueryClient } from '@tanstack/react-query';
import { lobbiesApi } from '../api/lobbiesApi';
import { queryKeys } from '../api/queryKeys';
import type { CreateLobbyRequest } from '../api/types';

/**
 * Uses `setQueryData` rather than `invalidateQueries`, matching `useJoinLobby`'s documented
 * exception (see TrashAnimal.Web/CLAUDE.md) — the create response's `lobby` field is already the
 * full authoritative LobbyView, and there is no prior cache entry to invalidate for a lobby that
 * didn't exist until this call succeeded.
 */
export function useCreateLobby() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: CreateLobbyRequest) => lobbiesApi.createLobby(request),
    onSuccess: (response) => {
      queryClient.setQueryData(queryKeys.lobby(response.lobby.lobbyId), response.lobby);
    },
  });
}
