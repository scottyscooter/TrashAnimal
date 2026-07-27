import { useCallback } from 'react';
import { useLocalStorage } from './useLocalStorage';

export interface ClientIdentity {
  lobbyId: string;
  seatIndex: number;
  clientToken: string;
  /** Set once the lobby starts and produces a real game, so `useGameClientIdentity` can resolve
   * the same identity by `gameId` after the player navigates to `/games/:gameId` (a route that
   * only has `gameId`, not `lobbyId`, available). */
  gameId?: string;
}

export const IDENTITY_STORAGE_KEY = 'trashanimal:identity';

/**
 * A single stored identity slot, scoped by lobbyId match on read — stale identity from a
 * previously visited lobby is never applied to a different one. `setIdentity` takes the target
 * lobbyId explicitly rather than relying on the hook's own `lobbyId` argument, since callers like
 * CreateSessionForm only learn the lobbyId from the mutation response, after the render that
 * captured this hook's closure.
 */
export function useClientIdentity(lobbyId?: string) {
  const [stored, setStored] = useLocalStorage<ClientIdentity | null>(IDENTITY_STORAGE_KEY, null);

  const identity = stored && lobbyId !== undefined && stored.lobbyId === lobbyId ? stored : null;

  const setIdentity = useCallback(
    (targetLobbyId: string, seatIndex: number, clientToken: string) => {
      setStored({ lobbyId: targetLobbyId, seatIndex, clientToken });
    },
    [setStored],
  );

  /** Merges `gameId` onto whatever identity is currently stored (a no-op if nothing is stored). */
  const setGameId = useCallback(
    (gameId: string) => {
      if (stored) {
        setStored({ ...stored, gameId });
      }
    },
    [stored, setStored],
  );

  const clearIdentity = useCallback(() => setStored(null), [setStored]);

  return { identity, setIdentity, setGameId, clearIdentity };
}
