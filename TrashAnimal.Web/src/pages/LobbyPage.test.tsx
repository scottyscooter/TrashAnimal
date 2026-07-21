import { describe, it, expect, vi, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import userEvent from '@testing-library/user-event';
import { render, screen, waitFor } from '../test/test-utils';
import * as router from 'react-router-dom';
import { server } from '../test/msw/server';
import { API_BASE_URL } from '../api/httpClient';
import type { LobbyView } from '../api/types';
import LobbyPage from './LobbyPage';

const LOBBY_ID = '11111111-1111-1111-1111-111111111111';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: vi.fn(),
    useParams: vi.fn(),
  };
});

function storeIdentity(seatIndex: number, clientToken: string) {
  localStorage.setItem(
    'trashanimal:identity',
    JSON.stringify({ lobbyId: LOBBY_ID, seatIndex, clientToken }),
  );
}

describe('LobbyPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(router.useParams).mockReturnValue({ lobbyId: LOBBY_ID } as ReturnType<
      typeof router.useParams
    >);
    vi.mocked(router.useNavigate).mockReturnValue(vi.fn());
  });

  it('renders lobby heading', async () => {
    render(<LobbyPage />);
    expect(await screen.findByRole('heading', { name: /lobby/i })).toBeInTheDocument();
  });

  it('shows a join form for a visitor with no stored identity', async () => {
    render(<LobbyPage />);
    expect(await screen.findByLabelText(/nickname/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /join lobby/i })).toBeInTheDocument();
  });

  it('joins the lobby and shows the seated view', async () => {
    const user = userEvent.setup();
    render(<LobbyPage />);

    await user.type(await screen.findByLabelText(/nickname/i), 'Bob');
    await user.click(screen.getByRole('button', { name: /join lobby/i }));

    await waitFor(() => {
      expect(screen.queryByLabelText(/nickname/i)).not.toBeInTheDocument();
    });
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('shows the seated view directly for a returning host, without the join form', async () => {
    storeIdentity(0, 'test-client-token');

    render(<LobbyPage />);

    expect(await screen.findByText('Alice')).toBeInTheDocument();
    expect(screen.queryByLabelText(/nickname/i)).not.toBeInTheDocument();
  });

  it('only shows the Start Game button to the host (seat 0) once a second seat is filled', async () => {
    storeIdentity(0, 'test-client-token');
    const twoSeatLobby: LobbyView = {
      lobbyId: LOBBY_ID,
      seats: [
        { seatIndex: 0, nickname: 'Alice' },
        { seatIndex: 1, nickname: 'Bob' },
      ],
      isStarted: false,
      gameId: null,
    };
    server.use(http.get(`${API_BASE_URL}/lobbies/:lobbyId`, () => HttpResponse.json(twoSeatLobby)));

    render(<LobbyPage />);

    expect(await screen.findByRole('button', { name: /start game/i })).toBeInTheDocument();
  });

  it('hides the Start Game button from a non-host seat', async () => {
    storeIdentity(1, 'test-client-token-2');

    render(<LobbyPage />);

    await screen.findByText('Alice');
    expect(screen.queryByRole('button', { name: /start game/i })).not.toBeInTheDocument();
  });

  it('hides the Start Game button from the host while only one seat is filled', async () => {
    storeIdentity(0, 'test-client-token');

    render(<LobbyPage />);

    await screen.findByText('Alice');
    expect(screen.queryByRole('button', { name: /start game/i })).not.toBeInTheDocument();
  });

  it('shows a not-found message when the lobby does not exist', async () => {
    server.use(
      http.get(`${API_BASE_URL}/lobbies/:lobbyId`, () => new HttpResponse(null, { status: 404 })),
    );

    render(<LobbyPage />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/could not be found/i);
  });

  it('navigates every seated player to the game board once the lobby starts', async () => {
    storeIdentity(0, 'test-client-token');
    const mockNavigate = vi.fn();
    vi.mocked(router.useNavigate).mockReturnValue(mockNavigate);

    const startedLobby: LobbyView = {
      lobbyId: LOBBY_ID,
      seats: [{ seatIndex: 0, nickname: 'Alice' }],
      isStarted: true,
      gameId: '22222222-2222-2222-2222-222222222222',
    };
    server.use(http.get(`${API_BASE_URL}/lobbies/:lobbyId`, () => HttpResponse.json(startedLobby)));

    render(<LobbyPage />);

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/games/22222222-2222-2222-2222-222222222222');
    });
  });
});
