import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '../../test/test-utils';
import type { HandCardView } from '../../api/types';
import PlayerHand from './PlayerHand';

const PHONE_LANDSCAPE_QUERY = '(orientation: landscape) and (max-height: 599px) and (pointer: coarse)';

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

  it('shows no badge and renders at full opacity for the out-of-turn case (playableAs and unplayableReason both null), but is still non-interactive', () => {
    const handCards = buildHand({ playableAs: null, unplayableReason: null });
    render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

    expect(screen.queryByRole('button', { name: /more information/i })).not.toBeInTheDocument();
    const card = screen.getByRole('button', { name: /shiny/i });
    expect(card).toHaveAttribute('aria-disabled', 'true');
    expect(card).toHaveAttribute('tabindex', '-1');
    // Not-your-turn is not the same as unplayable-for-a-reason: no dimming, so players can still
    // read their own hand normally while waiting their turn.
    const cardImageWrapper = screen.getByAltText('Shiny').parentElement;
    expect(cardImageWrapper).not.toHaveStyle({ opacity: '0.55' });
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

  describe('on phone landscape', () => {
    const originalMatchMedia = window.matchMedia;

    beforeEach(() => {
      // Real touch devices never fire mouseenter/mouseleave, so `isFanned` on phone landscape is
      // forced true independent of hover — see the mobile landscape plan, Round 2 Finding 2. This
      // stubs matchMedia to report phone landscape for every query, matching how
      // useLandscapeBreakpoint.test.ts mocks the same hook's own tests.
      window.matchMedia = vi.fn().mockImplementation((query: string) => ({
        matches: query === PHONE_LANDSCAPE_QUERY,
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }));
    });

    afterEach(() => {
      window.matchMedia = originalMatchMedia;
    });

    it('spreads cards at the fanned spacing without any hover, since touch has no hover state', () => {
      const handCards: HandCardView[] = [
        { cardId: 'shiny-1', name: 'Shiny', playableAs: null, unplayableReason: null },
        { cardId: 'feesh-1', name: 'Feesh', playableAs: null, unplayableReason: null },
      ];
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [firstCard, secondCard] = screen.getAllByRole('button', { name: /shiny|feesh/i });

      // Desktop's unfanned resting spacing is 90px (see the "keeps hover-to-fan alive" test above
      // for the hover-triggered 177px case) — phone landscape's always-fanned spacing is 62px,
      // deliberately distinct from both of desktop's values. Two cards, centered at offsets -0.5
      // and +0.5, land 62px apart without any mouseEnter having fired.
      expect(firstCard.style.left).toBe('calc(50% - 31px)');
      expect(secondCard.style.left).toBe('calc(50% + 31px)');
    });

    it('uses the smaller phone-landscape card size regardless of hover', () => {
      const handCards = buildHand({ playableAs: null, unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      expect(card.style.width).toBe('100px');
      expect(card.style.height).toBe('140px');
    });
  });
});
