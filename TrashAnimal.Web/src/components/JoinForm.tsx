import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useJoinLobby } from '../hooks/useJoinLobby';
import { useClientIdentity } from '../hooks/useClientIdentity';
import { ApiError } from '../api/httpClient';

const MAX_NICKNAME_LENGTH = 24;

interface JoinFormProps {
  lobbyId: string;
}

function JoinForm({ lobbyId }: JoinFormProps) {
  const [nickname, setNickname] = useState('');
  const nicknameInputRef = useRef<HTMLInputElement>(null);
  const joinLobby = useJoinLobby(lobbyId);
  const { setIdentity } = useClientIdentity(lobbyId);

  const isNicknameEmpty = nickname.trim().length === 0;

  // LobbiesController returns a bare-string body with no error code (see TrashAnimal.Web/CLAUDE.md),
  // so detecting "duplicate nickname" specifically means substring-matching its known message text.
  const isDuplicateNicknameError =
    joinLobby.error instanceof ApiError && joinLobby.error.message.toLowerCase().includes('nickname');

  useEffect(() => {
    if (isDuplicateNicknameError) {
      nicknameInputRef.current?.focus();
    }
  }, [isDuplicateNicknameError, joinLobby.error]);

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = nickname.trim();
    if (trimmed.length === 0) return;

    joinLobby.mutate(
      { nickname: trimmed },
      {
        onSuccess: (response) => {
          setIdentity(lobbyId, response.seatIndex, response.clientToken);
        },
      },
    );
  }

  const errorMessage =
    joinLobby.error instanceof ApiError
      ? joinLobby.error.message
      : joinLobby.error
        ? 'Something went wrong. Please try again.'
        : null;

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-3">
      <label htmlFor="join-nickname" className="text-sm font-medium">
        Nickname
      </label>
      <input
        id="join-nickname"
        ref={nicknameInputRef}
        type="text"
        value={nickname}
        maxLength={MAX_NICKNAME_LENGTH}
        onChange={(event) => setNickname(event.target.value)}
        placeholder="Your name"
        aria-invalid={isDuplicateNicknameError || undefined}
        className="rounded-md border border-[var(--border)] bg-transparent px-3 py-2 text-base outline-none focus:border-[var(--accent)]"
        required
      />
      {errorMessage && (
        <p role="alert" className="text-sm text-red-600">
          {errorMessage}
        </p>
      )}
      <button
        type="submit"
        disabled={joinLobby.isPending || isNicknameEmpty}
        className="rounded-md bg-[var(--accent)] px-4 py-2 font-medium text-white disabled:opacity-50"
      >
        {joinLobby.isPending ? 'Joining…' : 'Join lobby'}
      </button>
    </form>
  );
}

export default JoinForm;
