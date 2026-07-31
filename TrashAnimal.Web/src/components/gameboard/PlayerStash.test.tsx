import { describe, it, expect } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import type { OwnStashView } from '../../api/types';
import PlayerStash from './PlayerStash';

function buildOwnStash(overrides: Partial<OwnStashView> = {}): OwnStashView {
  return {
    faceDownCards: [],
    faceUpCards: [],
    ...overrides,
  };
}

describe('PlayerStash', () => {
  it('opens a modal listing the face-down cards by name when the face-down column is clicked', async () => {
    const user = userEvent.setup();
    render(
      <PlayerStash
        ownStash={buildOwnStash({
          faceDownCards: [
            { cardId: 'card-1', name: 'Nanners' },
            { cardId: 'card-2', name: 'Blammo' },
          ],
        })}
      />,
    );

    await user.click(screen.getByRole('button', { name: /face-down stash/i }));

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Your Face-Down Stash');
    expect(screen.getByAltText('Nanners')).toBeInTheDocument();
    expect(screen.getByAltText('Blammo')).toBeInTheDocument();
  });

  it('disables the face-down column when there are no face-down cards', () => {
    render(<PlayerStash ownStash={buildOwnStash({ faceDownCards: [] })} />);

    const faceDownButtons = screen.getAllByRole('button').filter((btn) => btn.hasAttribute('disabled'));
    expect(faceDownButtons.length).toBeGreaterThan(0);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('still opens the face-up stash modal independently of the face-down one', async () => {
    const user = userEvent.setup();
    render(
      <PlayerStash
        ownStash={buildOwnStash({
          faceDownCards: [{ cardId: 'card-1', name: 'Nanners' }],
          faceUpCards: [{ cardId: 'card-2', name: 'Shiny' }],
        })}
      />,
    );

    await user.click(screen.getByAltText('Shiny'));

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveTextContent('Your Face-Up Stash');
    expect(screen.queryByText('Your Face-Down Stash')).not.toBeInTheDocument();
  });
});
