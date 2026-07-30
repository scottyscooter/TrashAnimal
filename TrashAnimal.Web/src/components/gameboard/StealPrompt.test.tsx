import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import type { GameAction, GameState, StealPhaseView } from '../../api/types';
import { STEAL_PICK_SLOT_UNREVEALED_LABEL } from '../../api/types';
import StealPrompt from './StealPrompt';

function buildStealPhase(overrides: Partial<StealPhaseView> = {}): StealPhaseView {
  return {
    stealingPlayerIndex: 1,
    stealingPlayerName: 'Rex',
    victimIndex: 0,
    victimName: 'Fido',
    initialStealTargetZone: 'Hand',
    thiefPickSlots: null,
    ...overrides,
  };
}

interface RenderStealPromptOptions {
  state: GameState;
  stealPhase: StealPhaseView;
  localSeatIndex: number;
  allowedActions?: GameAction[];
  onAction?: (action: GameAction) => void;
  onCardPick?: (cardId: string) => void;
  isPending?: boolean;
}

function renderStealPrompt({
  state,
  stealPhase,
  localSeatIndex,
  allowedActions = [],
  onAction = () => {},
  onCardPick = () => {},
  isPending = false,
}: RenderStealPromptOptions) {
  return render(
    <StealPrompt
      state={state}
      stealPhase={stealPhase}
      localSeatIndex={localSeatIndex}
      allowedActions={allowedActions}
      onAction={onAction}
      onCardPick={onCardPick}
      isPending={isPending}
    />,
  );
}

describe('StealPrompt', () => {
  it('renders the victim response prompt when the local player is the victim', async () => {
    const user = userEvent.setup();
    const onAction = vi.fn();

    renderStealPrompt({
      state: 'AwaitingStealResponse',
      stealPhase: buildStealPhase(),
      localSeatIndex: 0,
      allowedActions: ['StealPlayDoggo', 'StealPlayKitteh', 'StealPass'],
      onAction,
    });

    expect(screen.getByText(/wants to steal from your hand/i)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /play doggo/i }));
    expect(onAction).toHaveBeenCalledWith('StealPlayDoggo');
  });

  it('renders the StealPickFan (not text buttons) when awaiting the thief card pick', () => {
    const slots = [
      { cardId: 'card-1', thiefFacingLabel: STEAL_PICK_SLOT_UNREVEALED_LABEL },
      { cardId: 'card-2', thiefFacingLabel: STEAL_PICK_SLOT_UNREVEALED_LABEL },
    ];

    renderStealPrompt({
      state: 'AwaitingStealCardPick',
      stealPhase: buildStealPhase({ thiefPickSlots: slots }),
      localSeatIndex: 1,
    });

    // The fan renders card-back tiles with an "Unrevealed card" alt, not raw label text buttons.
    expect(screen.getAllByAltText('Unrevealed card')).toHaveLength(2);
    expect(screen.queryByText(STEAL_PICK_SLOT_UNREVEALED_LABEL)).not.toBeInTheDocument();
  });

  it('calls onCardPick with the chosen cardId when a fan tile is clicked', async () => {
    const user = userEvent.setup();
    const onCardPick = vi.fn();
    const slots = [{ cardId: 'card-1', thiefFacingLabel: 'Blammo' }];

    renderStealPrompt({
      state: 'AwaitingStealCardPick',
      stealPhase: buildStealPhase({ thiefPickSlots: slots }),
      localSeatIndex: 1,
      onCardPick,
    });

    await user.click(screen.getByAltText('Blammo').closest('button')!);
    expect(onCardPick).toHaveBeenCalledWith('card-1');
  });

  it('disables fan tiles when isPending is true', () => {
    const slots = [{ cardId: 'card-1', thiefFacingLabel: 'Blammo' }];

    renderStealPrompt({
      state: 'AwaitingStealCardPick',
      stealPhase: buildStealPhase({ thiefPickSlots: slots }),
      localSeatIndex: 1,
      isPending: true,
    });

    expect(screen.getByAltText('Blammo').closest('button')).toBeDisabled();
  });

  it('renders the waiting state for players who are neither victim nor thief', () => {
    renderStealPrompt({
      state: 'AwaitingStealCardPick',
      stealPhase: buildStealPhase({ thiefPickSlots: [] }),
      localSeatIndex: 2,
    });

    expect(screen.getByText(/is attempting to steal from/i)).toBeInTheDocument();
  });
});
