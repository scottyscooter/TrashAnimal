import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import type { StealPickSlot } from '../../api/types';
import { STEAL_PICK_SLOT_UNREVEALED_LABEL } from '../../api/types';
import StealPickFan from './StealPickFan';

describe('StealPickFan', () => {
  it('renders unrevealed slots with card-back image and alt text', () => {
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: STEAL_PICK_SLOT_UNREVEALED_LABEL },
      { cardId: 'card-2', thiefFacingLabel: STEAL_PICK_SLOT_UNREVEALED_LABEL },
    ];

    render(<StealPickFan slots={slots} onPick={() => {}} isPending={false} />);

    const images = screen.getAllByAltText('Unrevealed card');
    expect(images).toHaveLength(2);
    images.forEach((img) => {
      expect(img).toHaveAttribute('src', expect.stringContaining('back'));
    });
  });

  it('renders revealed face-up slots with card image and alt text', () => {
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: 'Blammo' },
      { cardId: 'card-2', thiefFacingLabel: 'Feesh' },
    ];

    render(<StealPickFan slots={slots} onPick={() => {}} isPending={false} />);

    expect(screen.getByAltText('Blammo')).toBeInTheDocument();
    expect(screen.getByAltText('Feesh')).toBeInTheDocument();
  });

  it('falls back to card-back image for unrecognized labels', () => {
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: 'UnknownCard' },
    ];

    render(<StealPickFan slots={slots} onPick={() => {}} isPending={false} />);

    const img = screen.getByAltText('UnknownCard');
    expect(img).toHaveAttribute('src', expect.stringContaining('back'));
  });

  it('calls onPick with cardId when a tile is clicked', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: 'Blammo' },
    ];

    render(<StealPickFan slots={slots} onPick={onPick} isPending={false} />);

    await user.click(screen.getByAltText('Blammo').closest('button')!);
    expect(onPick).toHaveBeenCalledWith('card-1');
  });

  it('disables all tiles when isPending is true', () => {
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: 'Blammo' },
      { cardId: 'card-2', thiefFacingLabel: 'Feesh' },
    ];

    render(<StealPickFan slots={slots} onPick={() => {}} isPending={true} />);

    const buttons = screen.getAllByRole('button');
    buttons.forEach((button) => {
      expect(button).toBeDisabled();
    });
  });

  it('does not call onPick when a disabled tile is clicked', async () => {
    const user = userEvent.setup();
    const onPick = vi.fn();
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: 'Blammo' },
    ];

    render(<StealPickFan slots={slots} onPick={onPick} isPending={true} />);

    const button = screen.getByAltText('Blammo').closest('button')!;
    await user.click(button);
    expect(onPick).not.toHaveBeenCalled();
  });

  it('renders a mix of unrevealed and revealed slots correctly', () => {
    const slots: StealPickSlot[] = [
      { cardId: 'card-1', thiefFacingLabel: STEAL_PICK_SLOT_UNREVEALED_LABEL },
      { cardId: 'card-2', thiefFacingLabel: 'Blammo' },
      { cardId: 'card-3', thiefFacingLabel: STEAL_PICK_SLOT_UNREVEALED_LABEL },
    ];

    render(<StealPickFan slots={slots} onPick={() => {}} isPending={false} />);

    expect(screen.getAllByAltText('Unrevealed card')).toHaveLength(2);
    expect(screen.getByAltText('Blammo')).toBeInTheDocument();
  });
});
