import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen, waitFor } from '../test/test-utils';
import * as router from 'react-router-dom';
import HomePage from './HomePage';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: vi.fn(),
  };
});

describe('HomePage', () => {
  it('renders home page heading', () => {
    vi.mocked(router.useNavigate).mockReturnValue(vi.fn());
    render(<HomePage />);
    expect(screen.getByRole('heading', { name: /trashanimal/i })).toBeInTheDocument();
  });

  it('renders the create-session form', () => {
    vi.mocked(router.useNavigate).mockReturnValue(vi.fn());
    render(<HomePage />);
    expect(screen.getByLabelText(/nickname/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create game/i })).toBeInTheDocument();
  });

  it('disables the create button while nickname is empty', () => {
    vi.mocked(router.useNavigate).mockReturnValue(vi.fn());
    render(<HomePage />);
    expect(screen.getByRole('button', { name: /create game/i })).toBeDisabled();
  });

  it('creates a lobby and navigates to the lobby page', async () => {
    const mockNavigate = vi.fn();
    vi.mocked(router.useNavigate).mockReturnValue(mockNavigate);
    const user = userEvent.setup();
    render(<HomePage />);

    await user.type(screen.getByLabelText(/nickname/i), 'Alice');
    await user.click(screen.getByRole('button', { name: /create game/i }));

    await waitFor(() => {
      expect(mockNavigate).toHaveBeenCalledWith('/games/11111111-1111-1111-1111-111111111111/lobby');
    });
  });
});
