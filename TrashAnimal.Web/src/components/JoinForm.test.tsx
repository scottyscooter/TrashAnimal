import { describe, it, expect, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import userEvent from '@testing-library/user-event';
import { render, screen, waitFor } from '../test/test-utils';
import { server } from '../test/msw/server';
import { API_BASE_URL } from '../api/httpClient';
import JoinForm from './JoinForm';

const LOBBY_ID = '11111111-1111-1111-1111-111111111111';

describe('JoinForm', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('disables the submit button until a nickname is entered', () => {
    render(<JoinForm lobbyId={LOBBY_ID} />);
    expect(screen.getByRole('button', { name: /join lobby/i })).toBeDisabled();
  });

  it('joins and persists the returned identity', async () => {
    const user = userEvent.setup();
    render(<JoinForm lobbyId={LOBBY_ID} />);

    await user.type(screen.getByLabelText(/nickname/i), 'Bob');
    await user.click(screen.getByRole('button', { name: /join lobby/i }));

    await waitFor(() => {
      expect(localStorage.getItem('trashanimal:identity')).not.toBeNull();
    });
    const identity = JSON.parse(localStorage.getItem('trashanimal:identity')!);
    expect(identity).toEqual({ lobbyId: LOBBY_ID, seatIndex: 1, clientToken: 'test-client-token-2' });
  });

  it('refocuses the nickname field on a duplicate-nickname error', async () => {
    server.use(
      http.post(
        `${API_BASE_URL}/lobbies/:lobbyId/players`,
        () => new HttpResponse('Nickname is already taken in this lobby.', { status: 409 }),
      ),
    );
    const user = userEvent.setup();
    render(<JoinForm lobbyId={LOBBY_ID} />);

    const nicknameInput = screen.getByLabelText(/nickname/i);
    await user.type(nicknameInput, 'Alice');
    await user.click(screen.getByRole('button', { name: /join lobby/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/already taken/i);
    await waitFor(() => expect(nicknameInput).toHaveFocus());
  });

  it('shows a generic inline error for a full lobby without refocusing', async () => {
    server.use(
      http.post(`${API_BASE_URL}/lobbies/:lobbyId/players`, () => new HttpResponse('Lobby is full.', { status: 409 })),
    );
    const user = userEvent.setup();
    render(<JoinForm lobbyId={LOBBY_ID} />);

    await user.type(screen.getByLabelText(/nickname/i), 'Zoe');
    await user.click(screen.getByRole('button', { name: /join lobby/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Lobby is full.');
  });
});
