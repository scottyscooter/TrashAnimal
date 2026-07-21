import { describe, it, expect, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import userEvent from '@testing-library/user-event';
import { render, screen, waitFor } from '../test/test-utils';
import { server } from '../test/msw/server';
import { API_BASE_URL } from '../api/httpClient';
import CreateSessionForm from './CreateSessionForm';

describe('CreateSessionForm', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('disables the submit button until a nickname is entered', () => {
    render(<CreateSessionForm />);
    expect(screen.getByRole('button', { name: /create game/i })).toBeDisabled();
  });

  it('creates a lobby and persists the returned identity', async () => {
    const user = userEvent.setup();
    render(<CreateSessionForm />);

    await user.type(screen.getByLabelText(/nickname/i), 'Alice');
    await user.click(screen.getByRole('button', { name: /create game/i }));

    await waitFor(() => {
      expect(localStorage.getItem('trashanimal:identity')).not.toBeNull();
    });
    const identity = JSON.parse(localStorage.getItem('trashanimal:identity')!);
    expect(identity).toEqual({
      lobbyId: '11111111-1111-1111-1111-111111111111',
      seatIndex: 0,
      clientToken: 'test-client-token',
    });
  });

  it('shows an inline error message when creation fails', async () => {
    server.use(
      http.post(`${API_BASE_URL}/lobbies`, () => new HttpResponse('Nickname must not be empty.', { status: 400 })),
    );
    const user = userEvent.setup();
    render(<CreateSessionForm />);

    await user.type(screen.getByLabelText(/nickname/i), 'Alice');
    await user.click(screen.getByRole('button', { name: /create game/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Nickname must not be empty.');
  });
});
