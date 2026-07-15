import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '../test/test-utils';
import * as router from 'react-router-dom';
import ResultsPage from './ResultsPage';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: vi.fn(),
    useParams: vi.fn(),
  };
});

describe('ResultsPage', () => {
  beforeEach(() => {
    vi.mocked(router.useParams).mockReturnValue({ gameId: 'demo-game' } as any);
  });

  it('renders results heading', () => {
    render(<ResultsPage />);
    expect(screen.getByRole('heading', { name: /results/i })).toBeInTheDocument();
  });

  it('displays game id from params', () => {
    render(<ResultsPage />);
    expect(screen.getByText(/demo-game/)).toBeInTheDocument();
  });

  it('renders play again button', () => {
    render(<ResultsPage />);
    expect(screen.getByRole('button', { name: /play again/i })).toBeInTheDocument();
  });

  it('navigates to home when play again button is clicked', () => {
    const mockNavigate = vi.fn();
    vi.mocked(router.useNavigate).mockReturnValue(mockNavigate);

    render(<ResultsPage />);
    const playAgainButton = screen.getByRole('button', { name: /play again/i });
    playAgainButton.click();

    expect(mockNavigate).toHaveBeenCalledWith('/');
  });
});
