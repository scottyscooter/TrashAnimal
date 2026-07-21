import { describe, it, expect } from 'vitest';
import { http, HttpResponse } from 'msw';
import { render, screen, waitFor, fireEvent } from '../test/test-utils';
import { server } from '../test/msw/server';
import { API_BASE_URL } from '../api/httpClient';
import StartGameButton from './StartGameButton';

const LOBBY_ID = '11111111-1111-1111-1111-111111111111';

describe('StartGameButton', () => {
  it('renders a Start game button', () => {
    render(<StartGameButton lobbyId={LOBBY_ID} clientToken="test-client-token" />);
    expect(screen.getByRole('button', { name: /start game/i })).toBeInTheDocument();
  });

  it('shows an inline error when the start request is rejected', async () => {
    server.use(
      http.post(
        `${API_BASE_URL}/lobbies/:lobbyId/start`,
        () => new HttpResponse('Only the lobby admin can start the game.', { status: 403 }),
      ),
    );

    render(<StartGameButton lobbyId={LOBBY_ID} clientToken="not-the-host-token" />);
    fireEvent.click(screen.getByRole('button', { name: /start game/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/only the lobby admin/i);
  });

  it('disables the button while the start request is pending', async () => {
    server.use(
      http.post(
        `${API_BASE_URL}/lobbies/:lobbyId/start`,
        async () => {
          await new Promise((resolve) => setTimeout(resolve, 50));
          return HttpResponse.json({ gameId: '22222222-2222-2222-2222-222222222222' });
        },
      ),
    );

    render(<StartGameButton lobbyId={LOBBY_ID} clientToken="test-client-token" />);

    const button = screen.getByRole('button', { name: /start game/i });
    fireEvent.click(button);

    await waitFor(() => expect(button).toBeDisabled());
  });
});
