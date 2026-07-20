import { useMutation, useQueryClient } from '@tanstack/react-query';
import { lobbiesApi } from '../api/lobbiesApi';
import { queryKeys } from '../api/queryKeys';
import type { JoinLobbyRequest } from '../api/types';

/**
 * Uses `setQueryData` rather than `invalidateQueries` on success — the default convention for
 * mutations here is `invalidateQueries` (see `TrashAnimal.Web/CLAUDE.md`); this is the documented
 * exception, since the join response's `lobby` field is already the full authoritative LobbyView
 * and writing it directly closes the gap between seating this player and LobbyHub's `LobbyUpdated`
 * push arriving (which every other seat's `useLobbySignalR` still relies on for the same update).
 */
export function useJoinLobby(lobbyId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: JoinLobbyRequest) => lobbiesApi.joinLobby(lobbyId, request),
    onSuccess: (response) => {
      queryClient.setQueryData(queryKeys.lobby(lobbyId), response.lobby);
    },
  });
}
