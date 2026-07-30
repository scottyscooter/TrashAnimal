import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '../../test/test-utils';
import type { HandCardView } from '../../api/types';
import PlayerHand from './PlayerHand';

function buildHand(overrides: Partial<HandCardView> = {}): HandCardView[] {
  return [{ cardId: 'shiny-1', name: 'Shiny', playableAs: null, unplayableReason: null, ...overrides }];
}

describe('PlayerHand', () => {
  it('shows the info badge with the card\'s own unplayableReason when it is not playable', async () => {
    const handCards = buildHand({ unplayableReason: 'No opponent has anything in their stash to steal.' });
    render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

    const badge = screen.getByRole('button', { name: /more information/i });
    // fireEvent so this exercises the click handler in isolation, without also simulating the
    // mouse hovering over the badge first (see InfoBadge.test.tsx for why that distinction
    // matters post-A4: a click while already visible via hover now dismisses, by design).
    fireEvent.click(badge);
    expect(await screen.findByRole('tooltip')).toHaveTextContent(
      'No opponent has anything in their stash to steal.',
    );
  });

  it('shows no badge when the card is playable', () => {
    const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
    render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

    expect(screen.queryByRole('button', { name: /more information/i })).not.toBeInTheDocument();
  });

  it('shows no badge for the out-of-turn case (playableAs and unplayableReason both null), and renders grayed-out', () => {
    const handCards = buildHand({ playableAs: null, unplayableReason: null });
    render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

    expect(screen.queryByRole('button', { name: /more information/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /shiny/i })).toHaveAttribute('aria-disabled', 'true');
  });

  it('marks an unplayable card aria-disabled, non-tabbable, and does not fire onCardActivate on click', () => {
    const onCardActivate = vi.fn();
    const handCards = buildHand({ playableAs: null, unplayableReason: 'Cards drawn during your current turn cannot be played.' });
    render(<PlayerHand handCards={handCards} onCardActivate={onCardActivate} />);

    const card = screen.getByRole('button', { name: /shiny/i });
    expect(card).toHaveAttribute('aria-disabled', 'true');
    expect(card).toHaveAttribute('tabindex', '-1');

    fireEvent.click(card);
    expect(onCardActivate).not.toHaveBeenCalled();
  });

  it('fires onCardActivate with the card when a playable card is clicked', () => {
    const onCardActivate = vi.fn();
    const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
    render(<PlayerHand handCards={handCards} onCardActivate={onCardActivate} />);

    const card = screen.getByRole('button', { name: /shiny/i });
    expect(card).toHaveAttribute('aria-disabled', 'false');
    expect(card).toHaveAttribute('tabindex', '0');

    fireEvent.click(card);
    expect(onCardActivate).toHaveBeenCalledTimes(1);
    expect(onCardActivate).toHaveBeenCalledWith(handCards[0]);
  });

  it('keeps hover-to-fan alive for an unplayable card (regression guard)', () => {
    const handCards: HandCardView[] = [
      { cardId: 'shiny-1', name: 'Shiny', playableAs: null, unplayableReason: 'Cards drawn during your current turn cannot be played.' },
      { cardId: 'feesh-1', name: 'Feesh', playableAs: 'PlayFeesh', unplayableReason: null },
    ];
    render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

    const cards = screen.getAllByRole('button', { name: /shiny|feesh/i });
    const [unplayableCard, otherCard] = cards;

    const leftBeforeHover = otherCard.style.left;

    // Hovering the unplayable card must still re-fan the whole hand (widen spacing) — a previous
    // pass deliberately fixed a bug where a native <button disabled> killed hover for the whole
    // fan; this asserts that per-card unplayability doesn't regress it the same way.
    fireEvent.mouseEnter(unplayableCard);

    expect(otherCard.style.left).not.toBe(leftBeforeHover);
  });
});
