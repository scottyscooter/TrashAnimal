import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '../test/test-utils';
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
    render(<HomePage />);
    expect(screen.getByRole('heading', { name: /trashanimal/i })).toBeInTheDocument();
  });

  it('renders create game button', () => {
    render(<HomePage />);
    expect(screen.getByRole('button', { name: /create game/i })).toBeInTheDocument();
  });

  it('navigates to lobby when create game button is clicked', () => {
    const mockNavigate = vi.fn();
    vi.mocked(router.useNavigate).mockReturnValue(mockNavigate);

    render(<HomePage />);
    const createButton = screen.getByRole('button', { name: /create game/i });
    createButton.click();

    expect(mockNavigate).toHaveBeenCalledWith('/games/demo-game/lobby');
  });
});
