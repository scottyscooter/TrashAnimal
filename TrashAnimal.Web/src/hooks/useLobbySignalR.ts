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
      onConnectionError: (error) => {
        console.error(`LobbyHub connection error for lobby ${lobbyId}:`, error);
      },
    })
      .then((sub) => {
        if (cancelled) {
          void sub.stop();
          return;
        }
        subscription = sub;
      })
      .catch(() => {
        // Initial connect/join failure — already reported via onConnectionError above.
      });

    return () => {
      cancelled = true;
      void subscription?.stop();
    };
  }, [lobbyId, queryClient]);
}
