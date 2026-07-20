import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { connectToLobbyHub, type HubSubscription } from '../api/signalRClient';
import { queryKeys } from '../api/queryKeys';

/**
 * Subscribes to LobbyHub for the lifetime of the calling component. LobbyUpdated pushes the full
 * LobbyView directly (no hidden-information constraint), so the cache is updated in place with no
 * REST round trip. On reconnect the lobby query is invalidated unconditionally rather than
 * comparing revisions — LobbyView has no Revision field (review note 3).
 */
export function useLobbySignalR(lobbyId: string) {
  const queryClient = useQueryClient();

  useEffect(() => {
    let cancelled = false;
    let subscription: HubSubscription | null = null;

    connectToLobbyHub(lobbyId, {
      onLobbyUpdated: (envelope) => {
        queryClient.setQueryData(queryKeys.lobby(lobbyId), envelope.lobby);
      },
      onReconnected: () => {
        void queryClient.invalidateQueries({ queryKey: queryKeys.lobby(lobbyId) });
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
  }, [lobbyId, queryClient]);
}
