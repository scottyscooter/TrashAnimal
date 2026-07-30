import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import RollStopControls from './RollStopControls';

describe('RollStopControls', () => {
  it('renders no PLAY NANNERS/PLAY BLAMMO buttons when busted, and the abandon button reads DRAW 1 & END TURN', () => {
    render(
      <RollStopControls
        allowedActions={['PlayNanners', 'PlayBlammo', 'AbandonBust']}
        onAction={() => {}}
        isPending={false}
      />,
    );

    expect(screen.queryByRole('button', { name: /play nanners/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /play blammo/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /draw 1 & end turn/i })).toBeInTheDocument();
  });

  it('dispatches AbandonBust when the abandon button is clicked', async () => {
    const user = userEvent.setup();
    const onAction = vi.fn();
    render(
      <RollStopControls allowedActions={['AbandonBust']} onAction={onAction} isPending={false} />,
    );

    await user.click(screen.getByRole('button', { name: /draw 1 & end turn/i }));
    expect(onAction).toHaveBeenCalledWith('AbandonBust');
  });

  it('renders no abandon button when AbandonBust is not allowed', () => {
    render(<RollStopControls allowedActions={['RollDie']} onAction={() => {}} isPending={false} />);

    expect(screen.queryByRole('button', { name: /draw 1 & end turn/i })).not.toBeInTheDocument();
  });
});
