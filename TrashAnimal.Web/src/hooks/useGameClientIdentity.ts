import { useLocalStorage } from './useLocalStorage';
import { IDENTITY_STORAGE_KEY, type ClientIdentity } from './useClientIdentity';

/**
 * Reads the same stored identity as `useClientIdentity`, but scoped by `gameId` match instead of
 * `lobbyId` — needed because `GameBoardPage` only has `gameId` in its route (a lobby's id and the
 * real game's id it eventually produces are distinct GUIDs, so `useClientIdentity`'s lobbyId scoping
 * can't resolve a seat here). Requires `LobbyPage` to have called `setGameId` when the lobby started.
 */
export function useGameClientIdentity(gameId?: string) {
  const [stored] = useLocalStorage<ClientIdentity | null>(IDENTITY_STORAGE_KEY, null);

  const identity = stored && gameId !== undefined && stored.gameId === gameId ? stored : null;

  return { identity };
}
