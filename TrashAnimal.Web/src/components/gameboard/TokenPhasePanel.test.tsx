import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import type { TokenPhaseView } from '../../api/types';
import TokenPhasePanel from './TokenPhasePanel';

function buildTokenPhase(overrides: Partial<TokenPhaseView> = {}): TokenPhaseView {
  return {
    step: 'ChoosingNextToken',
    remainingTokens: [],
    activeToken: 'Steal',
    banditRevealedCardName: null,
    banditCurrentResponderIndex: null,
    stashableHandCardsForCurrentPrompt: [],
    recycleReplacementOptions: [],
    ...overrides,
  };
}

describe('TokenPhasePanel', () => {
  it('renders the repeat prompt and calls onStartSteal when step is StealChoosingVictim', async () => {
    const user = userEvent.setup();
    const onStartSteal = vi.fn();
    render(
      <TokenPhasePanel
        tokenPhase={buildTokenPhase({ step: 'StealChoosingVictim' })}
        allowedActions={['ResolveTokenSteal']}
        isPending={false}
        onAction={() => {}}
        onCardPick={() => {}}
        onDoubleStashSubmit={() => {}}
        onRecyclePick={() => {}}
        onStartSteal={onStartSteal}
      />,
    );

    expect(screen.getByText(/steal again/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /choose a player/i }));
    expect(onStartSteal).toHaveBeenCalledOnce();
  });

  it('renders nothing for StealChoosingVictim on other steps', () => {
    render(
      <TokenPhasePanel
        tokenPhase={buildTokenPhase({ step: 'RecycleChoosingReplacement' })}
        allowedActions={[]}
        isPending={false}
        onAction={() => {}}
        onCardPick={() => {}}
        onDoubleStashSubmit={() => {}}
        onRecyclePick={() => {}}
        onStartSteal={() => {}}
      />,
    );

    expect(screen.queryByText(/steal again/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /choose a player/i })).not.toBeInTheDocument();
  });

  it('hides the "Stash a card" button when TokenStashTrashStashMode is absent from allowedActions', () => {
    render(
      <TokenPhasePanel
        tokenPhase={buildTokenPhase({ step: 'StashTrashChooseBranch' })}
        allowedActions={['TokenStashTrashDrawOne']}
        isPending={false}
        onAction={() => {}}
        onCardPick={() => {}}
        onDoubleStashSubmit={() => {}}
        onRecyclePick={() => {}}
        onStartSteal={() => {}}
      />,
    );

    expect(screen.getByRole('button', { name: /draw a card/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /stash a card/i })).not.toBeInTheDocument();
  });

  it('shows the "Stash a card" button when TokenStashTrashStashMode is present in allowedActions', () => {
    render(
      <TokenPhasePanel
        tokenPhase={buildTokenPhase({ step: 'StashTrashChooseBranch' })}
        allowedActions={['TokenStashTrashDrawOne', 'TokenStashTrashStashMode']}
        isPending={false}
        onAction={() => {}}
        onCardPick={() => {}}
        onDoubleStashSubmit={() => {}}
        onRecyclePick={() => {}}
        onStartSteal={() => {}}
      />,
    );

    expect(screen.getByRole('button', { name: /draw a card/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /stash a card/i })).toBeInTheDocument();
  });
});
