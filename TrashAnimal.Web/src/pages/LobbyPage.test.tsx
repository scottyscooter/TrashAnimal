import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '../test/test-utils';
import * as router from 'react-router-dom';
import LobbyPage from './LobbyPage';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: vi.fn(),
    useParams: vi.fn(),
  };
});

describe('LobbyPage', () => {
  beforeEach(() => {
    vi.mocked(router.useParams).mockReturnValue({ lobbyId: 'demo-game' } as any);
  });

  it('renders lobby heading', () => {
    render(<LobbyPage />);
    expect(screen.getByRole('heading', { name: /lobby/i })).toBeInTheDocument();
  });

  it('displays game id from params', () => {
    render(<LobbyPage />);
    expect(screen.getByText(/demo-game/)).toBeInTheDocument();
  });

  it('renders start game button', () => {
    render(<LobbyPage />);
    expect(screen.getByRole('button', { name: /start game/i })).toBeInTheDocument();
  });

  it('navigates to game board when start game button is clicked', () => {
    const mockNavigate = vi.fn();
    vi.mocked(router.useNavigate).mockReturnValue(mockNavigate);

    render(<LobbyPage />);
    const startButton = screen.getByRole('button', { name: /start game/i });
    startButton.click();

    expect(mockNavigate).toHaveBeenCalledWith('/games/demo-game');
  });
});
