import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useCreateLobby } from '../hooks/useCreateLobby';
import { useClientIdentity } from '../hooks/useClientIdentity';
import { ApiError } from '../api/httpClient';

const MAX_NICKNAME_LENGTH = 24;

function CreateSessionForm() {
  const [nickname, setNickname] = useState('');
  const navigate = useNavigate();
  const createLobby = useCreateLobby();
  const { setIdentity } = useClientIdentity();

  const isNicknameEmpty = nickname.trim().length === 0;

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = nickname.trim();
    if (trimmed.length === 0) return;

    createLobby.mutate(
      { nickname: trimmed },
      {
        onSuccess: (response) => {
          setIdentity(response.lobby.lobbyId, response.seatIndex, response.clientToken);
          navigate(`/games/${response.lobby.lobbyId}/lobby`);
        },
      },
    );
  }

  const errorMessage =
    createLobby.error instanceof ApiError
      ? createLobby.error.message
      : createLobby.error
        ? 'Something went wrong. Please try again.'
        : null;

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-3">
      <label htmlFor="create-nickname" className="text-sm font-medium">
        Nickname
      </label>
      <input
        id="create-nickname"
        type="text"
        value={nickname}
        maxLength={MAX_NICKNAME_LENGTH}
        onChange={(event) => setNickname(event.target.value)}
        placeholder="Your name"
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
        disabled={createLobby.isPending || isNicknameEmpty}
        className="rounded-md bg-[var(--accent)] px-4 py-2 font-medium text-white disabled:opacity-50"
      >
        {createLobby.isPending ? 'Creating…' : 'Create game'}
      </button>
    </form>
  );
}

export default CreateSessionForm;
