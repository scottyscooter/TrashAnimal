import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '../test/test-utils';
import * as router from 'react-router-dom';
import GameBoardPage from './GameBoardPage';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: vi.fn(),
    useParams: vi.fn(),
  };
});

describe('GameBoardPage', () => {
  beforeEach(() => {
    vi.mocked(router.useParams).mockReturnValue({ gameId: 'demo-game' } as any);
  });

  it('renders game board heading', () => {
    render(<GameBoardPage />);
    expect(screen.getByRole('heading', { name: /game board/i })).toBeInTheDocument();
  });

  it('displays game id from params', () => {
    render(<GameBoardPage />);
    expect(screen.getByText(/demo-game/)).toBeInTheDocument();
  });

  it('renders end game button', () => {
    render(<GameBoardPage />);
    expect(screen.getByRole('button', { name: /end game/i })).toBeInTheDocument();
  });

  it('navigates to results when end game button is clicked', () => {
    const mockNavigate = vi.fn();
    vi.mocked(router.useNavigate).mockReturnValue(mockNavigate);

    render(<GameBoardPage />);
    const endButton = screen.getByRole('button', { name: /end game/i });
    endButton.click();

    expect(mockNavigate).toHaveBeenCalledWith('/games/demo-game/result');
  });
});
