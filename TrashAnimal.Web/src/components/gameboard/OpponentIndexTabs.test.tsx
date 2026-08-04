import { describe, it, expect } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import type { GameView, OpponentSummaryView } from '../../api/types';
import OpponentIndexTabs from './OpponentIndexTabs';

function buildOpponent(overrides: Partial<OpponentSummaryView> = {}): OpponentSummaryView {
  return {
    seatIndex: 1,
    name: 'Bob',
    handCount: 3,
    stashFaceDownCount: 1,
    stashFaceUpCards: [],
    ...overrides,
  };
}

function buildGameView(overrides: Partial<GameView> = {}): GameView {
  return {
    state: 'RollPhase',
    currentPlayerIndex: 0,
    currentPlayerName: 'Alice',
    isBusted: false,
    forcedRollRemaining: false,
    phaseOneTokens: [],
    handCards: [],
    yumYumResponderIndex: null,
    yumYumResponderName: null,
    stealPhase: null,
    tokenPhase: null,
    opponents: [buildOpponent()],
    deckCount: 30,
    discardPile: [],
    ownStash: { faceDownCards: [], faceUpCards: [] },
    log: [],
    ...overrides,
  };
}

describe('OpponentIndexTabs', () => {
  it('renders one tab per opponent', () => {
    const gameView = buildGameView({
      opponents: [buildOpponent({ seatIndex: 1, name: 'Bob' }), buildOpponent({ seatIndex: 2, name: 'Cleo' })],
    });

    render(<OpponentIndexTabs gameView={gameView} />);

    expect(screen.getByRole('button', { name: /view bob/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /view cleo/i })).toBeInTheDocument();
  });

  it('opens OpponentDetailModal targeted at the tapped opponent, regardless of which tab is highlighted', async () => {
    const user = userEvent.setup();
    const gameView = buildGameView({
      currentPlayerIndex: 0, // local player's turn, so the first opponent (Bob) is highlighted
      opponents: [buildOpponent({ seatIndex: 1, name: 'Bob' }), buildOpponent({ seatIndex: 2, name: 'Cleo' })],
    });

    render(<OpponentIndexTabs gameView={gameView} />);

    await user.click(screen.getByRole('button', { name: /view cleo/i }));

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Cleo');
    expect(dialog).not.toHaveTextContent('Bob');
  });

  it('lets every tab open its own opponent independently, not just the highlighted one', async () => {
    const user = userEvent.setup();
    const gameView = buildGameView({
      currentPlayerIndex: 2, // Cleo's turn, so Cleo's tab is contextually highlighted
      opponents: [buildOpponent({ seatIndex: 1, name: 'Bob' }), buildOpponent({ seatIndex: 2, name: 'Cleo' })],
    });

    render(<OpponentIndexTabs gameView={gameView} />);

    await user.click(screen.getByRole('button', { name: /view bob/i }));

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Bob');
  });

  it('closes the modal via the close button', async () => {
    const user = userEvent.setup();
    const gameView = buildGameView({ opponents: [buildOpponent({ seatIndex: 1, name: 'Bob' })] });

    render(<OpponentIndexTabs gameView={gameView} />);

    await user.click(screen.getByRole('button', { name: /view bob/i }));
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /close/i }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });
});
