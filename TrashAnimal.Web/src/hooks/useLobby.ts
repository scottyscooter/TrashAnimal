import { useQuery } from '@tanstack/react-query';
import { lobbiesApi } from '../api/lobbiesApi';
import { queryKeys } from '../api/queryKeys';

export function useLobby(lobbyId: string) {
  return useQuery({
    queryKey: queryKeys.lobby(lobbyId),
    queryFn: () => lobbiesApi.getLobby(lobbyId),
  });
}
