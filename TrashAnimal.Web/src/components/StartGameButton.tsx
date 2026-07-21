import { useStartLobby } from '../hooks/useStartLobby';
import { ApiError } from '../api/httpClient';

interface StartGameButtonProps {
  lobbyId: string;
  clientToken: string;
}

/**
 * Fires the start mutation only — it does not navigate. LobbyPage's own effect watches
 * `lobby.isStarted`/`lobby.gameId` (refreshed here via `useStartLobby`'s cache invalidation, and
 * for every other seat via `useLobbySignalR`'s push) and navigates every seated player uniformly,
 * so the host isn't special-cased for the transition into the real game.
 */
function StartGameButton({ lobbyId, clientToken }: StartGameButtonProps) {
  const startLobby = useStartLobby(lobbyId);

  function handleClick() {
    startLobby.mutate({ clientToken });
  }

  const errorMessage =
    startLobby.error instanceof ApiError
      ? startLobby.error.message
      : startLobby.error
        ? 'Something went wrong. Please try again.'
        : null;

  return (
    <div className="flex flex-col gap-2">
      <button
        type="button"
        onClick={handleClick}
        disabled={startLobby.isPending}
        className="rounded-md bg-[var(--accent)] px-4 py-2 font-medium text-white disabled:opacity-50"
      >
        {startLobby.isPending ? 'Starting…' : 'Start game'}
      </button>
      {errorMessage && (
        <p role="alert" className="text-sm text-red-600">
          {errorMessage}
        </p>
      )}
    </div>
  );
}

export default StartGameButton;
