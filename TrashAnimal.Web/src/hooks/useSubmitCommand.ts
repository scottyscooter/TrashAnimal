import { useMutation, useQueryClient } from '@tanstack/react-query';
import { gamesApi } from '../api/gamesApi';
import { queryKeys } from '../api/queryKeys';
import type { SubmitCommandRequest } from '../api/types';

/**
 * Resolves with the parsed GameCommandResponse for both success and the 422 rule-rejection case —
 * callers branch on `succeeded` rather than catching. On success the game view query is
 * invalidated so its cached Revision comes from a fresh GET (GameCommandResponse itself carries no
 * revision field).
 */
export function useSubmitCommand(gameId: string, playerSeat: number) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (request: SubmitCommandRequest) => gamesApi.submitCommand(gameId, request),
    onSuccess: (response) => {
      if (response.succeeded) {
        void queryClient.invalidateQueries({ queryKey: queryKeys.gameView(gameId, playerSeat) });
      }
    },
  });
}
