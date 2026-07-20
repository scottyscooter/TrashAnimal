import { useQuery } from '@tanstack/react-query';
import { gamesApi } from '../api/gamesApi';
import { queryKeys } from '../api/queryKeys';

export function useGameView(gameId: string, playerSeat: number) {
  return useQuery({
    queryKey: queryKeys.gameView(gameId, playerSeat),
    queryFn: () => gamesApi.getView(gameId, playerSeat),
  });
}
