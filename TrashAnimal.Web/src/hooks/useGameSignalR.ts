import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { connectToGameHub, type HubSubscription } from '../api/signalRClient';
import { gamesApi } from '../api/gamesApi';
import { queryKeys } from '../api/queryKeys';
import type { PlayerViewResponse } from '../api/types';

/**
 * Subscribes to GameHub for the lifetime of the calling component. GameHub is push-only — every
 * GameUpdated notification triggers a REST re-fetch (never trusts hub payload as game state,
 * preserving the hidden-information boundary). On reconnect, compares the cached
 * PlayerViewResponse.Revision against a freshly-fetched view's revision and only updates the cache
 * if they differ, per GameHub's documented reconnect protocol.
 */
export function useGameSignalR(gameId: string, playerSeat: number) {
  const queryClient = useQueryClient();

  useEffect(() => {
    let cancelled = false;
    let subscription: HubSubscription | null = null;
    const queryKey = queryKeys.gameView(gameId, playerSeat);

    connectToGameHub(gameId, {
      onGameUpdated: () => {
        void queryClient.invalidateQueries({ queryKey });
      },
      onReconnected: async () => {
        const cached = queryClient.getQueryData<PlayerViewResponse>(queryKey);
        const fresh = await gamesApi.getView(gameId, playerSeat);
        if (!cached || fresh.revision !== cached.revision) {
          queryClient.setQueryData(queryKey, fresh);
        }
      },
    }).then((sub) => {
      if (cancelled) {
        void sub.stop();
        return;
      }
      subscription = sub;
    });

    return () => {
      cancelled = true;
      void subscription?.stop();
    };
  }, [gameId, playerSeat, queryClient]);
}
