import { useQuery } from '@tanstack/react-query';
import { gamesApi } from '../api/gamesApi';
import { queryKeys } from '../api/queryKeys';

export function useGameResult(gameId: string) {
  return useQuery({
    queryKey: queryKeys.gameResult(gameId),
    queryFn: () => gamesApi.getResult(gameId),
  });
}
