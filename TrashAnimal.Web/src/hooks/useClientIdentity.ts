import { useCallback } from 'react';
import { useLocalStorage } from './useLocalStorage';

export interface ClientIdentity {
  lobbyId: string;
  seatIndex: number;
  clientToken: string;
}

const STORAGE_KEY = 'trashanimal:identity';

/**
 * A single stored identity slot, scoped by lobbyId match on read — stale identity from a
 * previously visited lobby is never applied to a different one. `setIdentity` takes the target
 * lobbyId explicitly rather than relying on the hook's own `lobbyId` argument, since callers like
 * CreateSessionForm only learn the lobbyId from the mutation response, after the render that
 * captured this hook's closure.
 */
export function useClientIdentity(lobbyId?: string) {
  const [stored, setStored] = useLocalStorage<ClientIdentity | null>(STORAGE_KEY, null);

  const identity = stored && lobbyId !== undefined && stored.lobbyId === lobbyId ? stored : null;

  const setIdentity = useCallback(
    (targetLobbyId: string, seatIndex: number, clientToken: string) => {
      setStored({ lobbyId: targetLobbyId, seatIndex, clientToken });
    },
    [setStored],
  );

  const clearIdentity = useCallback(() => setStored(null), [setStored]);

  return { identity, setIdentity, clearIdentity };
}
