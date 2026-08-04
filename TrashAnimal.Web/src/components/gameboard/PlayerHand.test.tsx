import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { act } from '@testing-library/react';
import { render, screen, fireEvent, within } from '../../test/test-utils';
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

  describe('hold-to-enlarge', () => {
    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    it('opens the enlarged card preview after holding a card past the hold duration', () => {
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));

      expect(screen.getByRole('dialog', { name: /shiny card, enlarged/i })).toBeInTheDocument();
    });

    it('does not open the preview or play the card on a quick tap shorter than the hold duration', () => {
      const onCardActivate = vi.fn();
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={onCardActivate} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(200));
      fireEvent.pointerUp(card);
      fireEvent.click(card);

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(onCardActivate).toHaveBeenCalledTimes(1);
    });

    it('cancels the pending hold if the pointer moves past the cancel threshold before it fires (a swipe attempt)', () => {
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      fireEvent.pointerMove(card, { clientX: 130, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('does not play the card when the hold fires, even though the pointer is still down over the card', () => {
      const onCardActivate = vi.fn();
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={onCardActivate} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));
      // Simulates touch's implicit pointer capture: the click that follows pointerup still
      // targets the original card element even though the overlay is now on top.
      fireEvent.click(card);

      expect(onCardActivate).not.toHaveBeenCalled();
    });

    it('closes the preview when clicking the scrim, and does not trigger the card underneath', () => {
      const onCardActivate = vi.fn();
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={onCardActivate} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));

      fireEvent.click(screen.getByRole('dialog', { name: /shiny card, enlarged/i }));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
      expect(onCardActivate).not.toHaveBeenCalled();
    });

    it('closes the preview via the close button', () => {
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));

      fireEvent.click(screen.getByRole('button', { name: /close/i }));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('closes the preview on Escape', () => {
      const handCards = buildHand({ playableAs: 'PlayShiny', unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('can hold-to-enlarge an unplayable card, and renders it at full color/opacity in the preview', () => {
      const handCards = buildHand({ playableAs: null, unplayableReason: 'Cards drawn during your current turn cannot be played.' });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      fireEvent.pointerDown(card, { clientX: 100, clientY: 100 });
      act(() => vi.advanceTimersByTime(500));

      const dialog = screen.getByRole('dialog', { name: /shiny card, enlarged/i });
      const previewImage = within(dialog).getByAltText('Shiny');
      expect(previewImage).not.toHaveStyle({ filter: 'grayscale(0.6)' });
      expect(previewImage.parentElement).not.toHaveStyle({ opacity: '0.55' });
    });
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
      // deliberately distinct from both of desktop's values. The carousel centers on a card index
      // (carouselIndex starts at 0), not the fan's midpoint, so the first card sits dead center
      // (offset 0) and the second one card-width of spacing to its right (offset 1).
      expect(firstCard.style.left).toBe('calc(50% + 0px)');
      expect(secondCard.style.left).toBe('calc(50% + 62px)');
    });

    it('uses the smaller phone-landscape card size regardless of hover', () => {
      const handCards = buildHand({ playableAs: null, unplayableReason: null });
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const card = screen.getByRole('button', { name: /shiny/i });
      expect(card.style.width).toBe('100px');
      expect(card.style.height).toBe('140px');
    });

    function buildCarouselHand(): HandCardView[] {
      return [
        { cardId: 'a', name: 'Shiny', playableAs: null, unplayableReason: null },
        { cardId: 'b', name: 'Feesh', playableAs: null, unplayableReason: null },
        { cardId: 'c', name: 'Nanners', playableAs: null, unplayableReason: null },
        { cardId: 'd', name: 'Yumyum', playableAs: null, unplayableReason: null },
      ];
    }

    function swipe(container: HTMLElement, distancePx: number, elapsedMs: number) {
      const now = vi.spyOn(performance, 'now');
      now.mockReturnValueOnce(0).mockReturnValueOnce(elapsedMs);
      fireEvent.touchStart(container, { touches: [{ clientX: 200 }] });
      // Positive distancePx = finger moves leftward (swipe left → carousel advances forward).
      fireEvent.touchEnd(container, { changedTouches: [{ clientX: 200 - distancePx }] });
      now.mockRestore();
    }

    it('shows the centered card plus one full neighbor on each side, one small faded "peek" card past that, and hides the rest', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first, second, third, fourth] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      // carouselIndex starts at 0 → offsets are 0, 1, 2, 3. VISIBLE_CARD_RADIUS=1 keeps offsets 0
      // and 1 fully visible; offset 2 is exactly one past the radius, so it peeks in small and
      // faded (PEEK_CARD_OPACITY) rather than vanishing outright; offset 3 is fully hidden.
      expect(first.style.opacity).toBe('1');
      expect(second.style.opacity).toBe('1');
      expect(third.style.opacity).toBe('0.45');
      expect(fourth.style.opacity).toBe('0');
    });

    it('a peeking card is not grayscale like an unplayable card would be — it stays full color, just small and faded', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const third = screen.getByRole('button', { name: /nanners/i }); // offset 2 → peeking
      expect(third.style.opacity).toBe('0.45');
      expect(third.style.transform).toContain('scale(0.8)');
      const cardImageWrapper = screen.getByAltText('Nanners').parentElement;
      // No grayscale/dim filter — that visual language is reserved for an actually-unplayable card
      // (see the "renders at full opacity for the out-of-turn case" test above), so a peek never
      // gets confused with "this card can't be played."
      expect(cardImageWrapper).not.toHaveStyle({ filter: 'grayscale(0.6)' });
    });

    it('a peeking or fully hidden card is non-tabbable and non-clickable even if it would otherwise be playable', () => {
      const handCards = buildCarouselHand();
      handCards[2] = { ...handCards[2], playableAs: 'PlayFeesh' }; // Nanners: peeking (offset 2)
      handCards[3] = { ...handCards[3], playableAs: 'PlayShiny' }; // Yumyum: fully hidden (offset 3)
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const third = screen.getByRole('button', { name: /nanners/i });
      expect(third).toHaveAttribute('tabindex', '-1');
      expect(third.style.pointerEvents).toBe('none');

      const fourth = screen.getByRole('button', { name: /yumyum/i });
      expect(fourth.style.opacity).toBe('0');
      expect(fourth).toHaveAttribute('tabindex', '-1');
      expect(fourth.style.pointerEvents).toBe('none');
    });

    it('a slow swipe left rotates the carousel forward by exactly one card', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      const touchLayer = first.parentElement!;

      // 50px of movement over 1000ms is well under the fast-flick velocity threshold (0.5px/ms).
      swipe(touchLayer, 50, 1000);

      const nanners = screen.getByRole('button', { name: /nanners/i });
      // carouselIndex moved from 0 to 1: Nanners (index 2) is now offset 1 from center, fully visible.
      expect(nanners.style.opacity).toBe('1');
      const yumyum = screen.getByRole('button', { name: /yumyum/i });
      // Yumyum (index 3) is now offset 2 — exactly one past the radius, so it peeks rather than
      // being fully hidden.
      expect(yumyum.style.opacity).toBe('0.45');
    });

    it('a fast flick rotates the carousel forward by multiple cards', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      const touchLayer = first.parentElement!;

      // 50px over 50ms = 1px/ms, well above the 0.5px/ms fast-flick threshold, so this should jump
      // 1 + FAST_FLICK_EXTRA_CARDS (2) = 3 cards forward, from index 0 to index 3 (clamped in range).
      swipe(touchLayer, 50, 50);

      const yumyum = screen.getByRole('button', { name: /yumyum/i });
      // carouselIndex 3: Yumyum (index 3) is now dead center.
      expect(yumyum.style.opacity).toBe('1');
      expect(yumyum.style.left).toBe('calc(50% + 0px)');
    });

    it('a swipe shorter than the distance threshold does not rotate the carousel', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      const touchLayer = first.parentElement!;

      swipe(touchLayer, 10, 1000);

      expect(first.style.left).toBe('calc(50% + 0px)');
    });

    it('swiping left at the last card clamps instead of going out of range', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      const touchLayer = first.parentElement!;

      swipe(touchLayer, 50, 50); // fast flick, would move 3 cards from index 0 — lands on index 3
      swipe(touchLayer, 50, 50); // another fast flick past the end should clamp at index 3

      const yumyum = screen.getByRole('button', { name: /yumyum/i });
      expect(yumyum.style.left).toBe('calc(50% + 0px)');
    });

    it('the ArrowRight key rotates the carousel forward by one card', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      const touchLayer = first.parentElement!;

      fireEvent.keyDown(touchLayer, { key: 'ArrowRight' });

      const feesh = screen.getByRole('button', { name: /feesh/i });
      expect(feesh.style.left).toBe('calc(50% + 0px)');
    });

    it('the ArrowLeft key at the first card clamps instead of going negative', () => {
      const handCards = buildCarouselHand();
      render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

      const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
      const touchLayer = first.parentElement!;

      fireEvent.keyDown(touchLayer, { key: 'ArrowLeft' });

      expect(first.style.left).toBe('calc(50% + 0px)');
    });

    describe('more-cards peek indicator', () => {
      it('peeks only the right neighbor at the start of the hand, since there is nothing before it', () => {
        const handCards = buildCarouselHand();
        render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

        // carouselIndex 0: offset -1 doesn't exist (no card before index 0), offset 2 (Nanners) peeks.
        const nanners = screen.getByRole('button', { name: /nanners/i });
        expect(nanners.style.opacity).toBe('0.45');
      });

      it('peeks a card on both sides once scrolled into the middle of the hand', () => {
        // 5 cards so there's a middle carouselIndex (2) with a peeking card on both sides — with
        // only 4 cards, radius 1 always covers one full edge, so both sides can never peek at once.
        const handCards: HandCardView[] = [
          ...buildCarouselHand(),
          { cardId: 'e', name: 'Blammo', playableAs: null, unplayableReason: null },
        ];
        render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

        const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum|blammo/i });
        const touchLayer = first.parentElement!;
        fireEvent.keyDown(touchLayer, { key: 'ArrowRight' });
        fireEvent.keyDown(touchLayer, { key: 'ArrowRight' }); // carouselIndex 0 -> 2

        // At carouselIndex 2: Shiny (index 0, offset -2) and Blammo (index 4, offset 2) both peek.
        expect(screen.getByRole('button', { name: /shiny/i }).style.opacity).toBe('0.45');
        expect(screen.getByRole('button', { name: /blammo/i }).style.opacity).toBe('0.45');
      });

      it('peeks only the left neighbor at the end of the hand, since there is nothing after it', () => {
        const handCards = buildCarouselHand();
        render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

        const [first] = screen.getAllByRole('button', { name: /shiny|feesh|nanners|yumyum/i });
        const touchLayer = first.parentElement!;
        fireEvent.keyDown(touchLayer, { key: 'ArrowRight' });
        fireEvent.keyDown(touchLayer, { key: 'ArrowRight' });
        fireEvent.keyDown(touchLayer, { key: 'ArrowRight' }); // carouselIndex 0 -> 3 (last card)

        // At carouselIndex 3 (last): Feesh (index 1, offset -2) peeks; there's no card past the end.
        expect(screen.getByRole('button', { name: /feesh/i }).style.opacity).toBe('0.45');
      });

      it('nothing peeks when the whole hand already fits within the visible radius', () => {
        const handCards = buildCarouselHand().slice(0, 2); // 2 cards, radius 1 covers both from index 0
        render(<PlayerHand handCards={handCards} onCardActivate={() => {}} />);

        for (const card of screen.getAllByRole('button', { name: /shiny|feesh/i })) {
          expect(card.style.opacity).toBe('1');
        }
      });
    });
  });
});
