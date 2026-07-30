import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '../../test/test-utils';
import type { HandCardView } from '../../api/types';
import PlayerHand from './PlayerHand';

const HAND_CARDS: HandCardView[] = [{ cardId: 'shiny-1', name: 'Shiny' }];

describe('PlayerHand', () => {
  it('shows the Shiny info badge with the given explanation when Shiny is not playable', async () => {
    render(
      <PlayerHand
        handCards={HAND_CARDS}
        allowedActions={[]}
        onFeeshClick={() => {}}
        shinyDisabledExplanation="No opponent has anything in their stash to steal."
      />,
    );

    const badge = screen.getByRole('button', { name: /more information/i });
    // fireEvent so this exercises the click handler in isolation, without also simulating the
    // mouse hovering over the badge first (see InfoBadge.test.tsx for why that distinction
    // matters post-A4: a click while already visible via hover now dismisses, by design).
    fireEvent.click(badge);
    expect(await screen.findByRole('tooltip')).toHaveTextContent(
      'No opponent has anything in their stash to steal.',
    );
  });

  it('shows no badge when Shiny is playable', () => {
    render(
      <PlayerHand
        handCards={HAND_CARDS}
        allowedActions={['PlayShiny']}
        onFeeshClick={() => {}}
        shinyDisabledExplanation={null}
      />,
    );

    expect(screen.queryByRole('button', { name: /more information/i })).not.toBeInTheDocument();
  });

  it('shows no badge when it is not the local player\'s turn (explanation computed as null upstream)', () => {
    render(
      <PlayerHand
        handCards={HAND_CARDS}
        allowedActions={[]}
        onFeeshClick={() => {}}
        shinyDisabledExplanation={null}
      />,
    );

    expect(screen.queryByRole('button', { name: /more information/i })).not.toBeInTheDocument();
  });
});
