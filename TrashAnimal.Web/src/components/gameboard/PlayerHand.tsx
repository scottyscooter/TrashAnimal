import { useEffect, useRef, useState } from 'react';
import type { KeyboardEvent, PointerEvent, TouchEvent } from 'react';
import type { HandCardView } from '../../api/types';
import { useIsPhoneLandscape } from '../../hooks/useLandscapeBreakpoint';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import CardZoomOverlay from './CardZoomOverlay';
import InfoBadge from '../InfoBadge';

/** How long a pointer must stay down on a card, roughly stationary, before it counts as a
 * "hold" that opens the enlarged card preview rather than a tap that plays the card. */
const HOLD_DURATION_MS = 450;

/** A pointer that drifts past this distance (px) before HOLD_DURATION_MS elapses cancels the
 * pending hold — this is what lets a real swipe attempt (phone-landscape carousel) start
 * without being misread as a hold. */
const HOLD_MOVE_CANCEL_THRESHOLD_PX = 10;

/** How many cards on either side of the carousel's centered card stay fully visible/interactive on
 * phone landscape (1 = 3 cards total, center + 1 neighbor each side). A single tunable knob so the
 * "dial" width can be adjusted without touching the swipe/render logic below. */
const VISIBLE_CARD_RADIUS = 1;

/** The one card just past VISIBLE_CARD_RADIUS on each side renders as a "peek" — small and faded,
 * signaling more cards that way — instead of vanishing outright. This is a distinct visual language
 * from an unplayable card (opacity 0.55 + grayscale, still full-size — see hasUnplayableReason
 * below): peek cards shrink and fade but keep full color, so "there's more this way" never reads as
 * "this card can't be played." */
const PEEK_CARD_OPACITY = 0.45;
const PEEK_CARD_SCALE = 0.8;

/** Minimum horizontal swipe distance (px) before it registers as a carousel rotation at all —
 * below this, treat it as a tap/no-op rather than an accidental nudge. */
const SWIPE_DISTANCE_THRESHOLD_PX = 30;

/** Swipe speed (px/ms) above which a flick rotates multiple cards instead of just one, so a fast
 * flick across a large hand doesn't take many repeated gestures to get across it. */
const FAST_FLICK_VELOCITY_PX_PER_MS = 0.5;
const FAST_FLICK_EXTRA_CARDS = 2;

interface PlayerHandProps {
  handCards: HandCardView[];
  /** Called with the card's own `playableAs` action when a playable card is activated (click or
   * Enter/Space). Never called for a card whose `playableAs` is null — the caller does not need to
   * re-check playability. Routing the action to the right handler (e.g. opening the Feesh discard
   * picker vs. the Shiny victim picker vs. dispatching a plain token-phase action) is the caller's
   * job, same as every other action-dispatch entry point in GameBoardPage. */
  onCardActivate: (card: HandCardView) => void;
}

function clampCarouselIndex(index: number, cardCount: number) {
  if (cardCount === 0) return 0;
  return Math.min(Math.max(index, 0), cardCount - 1);
}

/** Desktop: fanned hand per the design — hover-spreads the whole fan, and the specifically-hovered
 * card additionally scales up and lifts to the front.
 *
 * Playability is per-card (`card.playableAs`/`card.unplayableReason`, driven by the backend's
 * ranked-reason contract on `HandCardView`) rather than one flag for the whole fan.
 *
 * Phone landscape: hover never fires on touch devices, so instead of a fan this renders as a
 * touch/keyboard-driven carousel — swiping or pressing the arrow keys rotates `carouselIndex`. The
 * centered card ± `VISIBLE_CARD_RADIUS` stays fully visible/interactive; the next card past that on
 * each side renders as a small, faded "peek" (see PEEK_CARD_OPACITY/SCALE) so there's more to
 * swipe toward; anything further out is fully hidden. `isFanned` (used for spacing/rotation/lift,
 * shared with desktop's hover-fanned state) is still forced permanently true here since the
 * carousel's spread values reuse the same "fanned" branch of those constants. */
function PlayerHand({ handCards, onCardActivate }: PlayerHandProps) {
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  // Which card is centered in the phone-landscape carousel. Unused on desktop, which keeps the
  // hover-driven fan instead — see isPhoneLandscape branch below.
  const [carouselIndex, setCarouselIndex] = useState(0);
  const touchStartXRef = useRef<number | null>(null);
  const touchStartTimeRef = useRef(0);

  // Card currently shown large in CardZoomOverlay, or null when no overlay is open. Only one
  // card can be zoomed at a time, so this is hoisted above the per-card map rather than kept
  // as per-card state.
  const [zoomedCard, setZoomedCard] = useState<HandCardView | null>(null);
  const holdTimeoutRef = useRef<number | null>(null);
  const holdStartPositionRef = useRef<{ x: number; y: number } | null>(null);
  // Set true the instant a hold fires (see startHold below) and checked by activate() so the
  // click that follows pointerup after a hold doesn't also play the card — touch's implicit
  // pointer capture means that click can still target the original card element even though
  // the zoom overlay is now the topmost thing on screen.
  const holdFiredRef = useRef(false);

  function clearPendingHold() {
    if (holdTimeoutRef.current !== null) {
      window.clearTimeout(holdTimeoutRef.current);
      holdTimeoutRef.current = null;
    }
    holdStartPositionRef.current = null;
  }

  function startHold(event: PointerEvent<HTMLDivElement>, card: HandCardView) {
    holdStartPositionRef.current = { x: event.clientX, y: event.clientY };
    holdTimeoutRef.current = window.setTimeout(() => {
      holdTimeoutRef.current = null;
      holdFiredRef.current = true;
      setZoomedCard(card);
    }, HOLD_DURATION_MS);
  }

  function handleHoldPointerMove(event: PointerEvent<HTMLDivElement>) {
    const start = holdStartPositionRef.current;
    if (!start) return;
    const distance = Math.hypot(event.clientX - start.x, event.clientY - start.y);
    if (distance > HOLD_MOVE_CANCEL_THRESHOLD_PX) {
      clearPendingHold();
    }
  }

  const count = handCards.length;
  const centerOffset = (count - 1) / 2;
  const isPhoneLandscape = useIsPhoneLandscape();
  const isFanned = isPhoneLandscape || hoveredIndex !== null;

  // Keep the carousel's centered index in range as the hand's size changes (cards played/drawn),
  // e.g. don't leave it pointing past the end after the hand shrinks.
  useEffect(() => {
    if (!isPhoneLandscape) return;
    setCarouselIndex((current) => clampCarouselIndex(current, count));
  }, [count, isPhoneLandscape]);

  function rotateCarousel(direction: 1 | -1, cardsToMove: number) {
    setCarouselIndex((current) => clampCarouselIndex(current + direction * cardsToMove, count));
  }

  function handleTouchStart(event: TouchEvent<HTMLDivElement>) {
    touchStartXRef.current = event.touches[0].clientX;
    touchStartTimeRef.current = performance.now();
  }

  function handleTouchEnd(event: TouchEvent<HTMLDivElement>) {
    const startX = touchStartXRef.current;
    touchStartXRef.current = null;
    if (startX === null) return;
    const endX = event.changedTouches[0].clientX;
    const distance = startX - endX;
    if (Math.abs(distance) < SWIPE_DISTANCE_THRESHOLD_PX) return;
    const elapsedMs = Math.max(performance.now() - touchStartTimeRef.current, 1);
    const velocity = Math.abs(distance) / elapsedMs;
    // A fast flick rotates several cards at once so crossing a large hand doesn't take many
    // repeated swipes; a slow drag still rotates exactly one card for precise navigation.
    const cardsToMove = velocity >= FAST_FLICK_VELOCITY_PX_PER_MS ? 1 + FAST_FLICK_EXTRA_CARDS : 1;
    rotateCarousel(distance > 0 ? 1 : -1, cardsToMove);
  }

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'ArrowRight') {
      event.preventDefault();
      rotateCarousel(1, 1);
    } else if (event.key === 'ArrowLeft') {
      event.preventDefault();
      rotateCarousel(-1, 1);
    }
  }

  // Phone-landscape spacing/card size are smaller than desktop's "fanned" values, not just its
  // "resting" ones — desktop's 177px fanned spacing assumes a 198px-wide card with room to spare;
  // phone landscape needs every card visible within a ~900px budget without a hover gesture, so it
  // gets its own compact-but-always-spread scale instead of borrowing the desktop hover values.
  const spacing = isPhoneLandscape ? 62 : isFanned ? 177 : 90;
  const rotationStep = isFanned ? 4 : 2;
  // Phone landscape gets its own, much smaller dome step than desktop's fanned 20px: only cards
  // within VISIBLE_CARD_RADIUS of the carousel's center are ever visible there, but the dome still
  // reads proportionally huge on a 140px-tall card at desktop's 20px/offset scale — 8px keeps the
  // same visual "dome" read without dominating the card.
  const liftStep = isPhoneLandscape ? 8 : isFanned ? 20 : 5;

  const cardWidth = isPhoneLandscape ? 100 : 198;
  const cardHeight = isPhoneLandscape ? 140 : 277;

  return (
    <div className="fixed bottom-[190px] left-1/2 z-10 h-[320px] w-[1050px] -translate-x-1/2 phone-landscape:bottom-auto phone-landscape:top-1/2 phone-landscape:-translate-y-1/2 phone-landscape:h-[220px] phone-landscape:w-[90%] phone-landscape:max-w-[900px]">
      {/* On phone landscape this is a touch/keyboard-driven carousel: swiping or pressing the
          arrow keys rotates `carouselIndex`, which is what "offset" is computed from below instead
          of the fixed fan center. Only the centered card ± VISIBLE_CARD_RADIUS stays fully visible
          and interactive; one more card past that on each side "peeks" in small and faded (see
          isPeeking below) so the hand reads as a dial you rotate through rather than a fan that
          keeps growing with hand size. Desktop is unchanged: no carousel state, no touch handlers,
          same hover-driven fan as before. */}
      <div
        className="absolute inset-x-0 bottom-0 h-full"
        style={{ touchAction: isPhoneLandscape ? 'none' : undefined }}
        onTouchStart={isPhoneLandscape ? handleTouchStart : undefined}
        onTouchEnd={isPhoneLandscape ? handleTouchEnd : undefined}
        onKeyDown={isPhoneLandscape ? handleKeyDown : undefined}
        tabIndex={isPhoneLandscape ? 0 : undefined}
        aria-label={
          isPhoneLandscape
            ? `Your hand, ${count} card${count === 1 ? '' : 's'}. Swipe or use the arrow keys to browse.`
            : undefined
        }
      >
        {handCards.map((card, index) => {
          const offset = isPhoneLandscape ? index - carouselIndex : index - centerOffset;
          const isHovered = !isPhoneLandscape && hoveredIndex === index;
          const rotation = offset * rotationStep;
          // Downward-opening arc (dome): the center card sits highest, cards drop further down the
          // further they are from center. Hovering lifts that one card up an additional 34px.
          const dropFromCenter = Math.abs(offset) * liftStep;
          const hoverLift = isHovered ? 34 : 0;
          const translateY = dropFromCenter - hoverLift;
          const scale = isHovered ? 1.16 : 1;
          const isPlayable = card.playableAs !== null;
          const distanceFromCenter = Math.abs(offset);
          const isWithinCarouselView = !isPhoneLandscape || distanceFromCenter <= VISIBLE_CARD_RADIUS;
          const isPeeking = isPhoneLandscape && distanceFromCenter === VISIBLE_CARD_RADIUS + 1;
          // A null unplayableReason alongside a null playableAs means "not your turn, nothing to
          // explain" (see HandCardPlayabilityProjector) rather than "unplayable for a specific
          // reason" — that case renders like any other card in your hand (no dim, no grayscale, no
          // badge) so players can still read their own hand normally while waiting their turn. Only
          // dim/badge a card that has an actual reason to report.
          const hasUnplayableReason = card.unplayableReason !== null;
          const cardImage = (
            <div
              className="h-full w-full overflow-hidden rounded-[14px]"
              style={hasUnplayableReason ? { opacity: 0.55, filter: 'grayscale(0.6)' } : undefined}
            >
              <img
                src={CARD_IMAGE_BY_NAME[card.name]}
                alt={card.name}
                fetchPriority="high"
                className="h-full w-full object-cover"
              />
            </div>
          );

          function activate() {
            if (holdFiredRef.current) {
              holdFiredRef.current = false;
              return;
            }
            if (isPlayable) {
              onCardActivate(card);
            }
          }

          return (
            // A native <button disabled> suppresses hover/mouseenter in most browsers, not just
            // click — since most cards are only playable in narrow circumstances, that made the
            // whole fan's hover effect dead almost all the time. Hover must stay live for every
            // card regardless of that card's own playability.
            <div
              key={card.cardId}
              role="button"
              tabIndex={isPlayable && isWithinCarouselView ? 0 : -1}
              aria-disabled={!isPlayable}
              onMouseEnter={() => setHoveredIndex(index)}
              onMouseLeave={() => setHoveredIndex((current) => (current === index ? null : current))}
              onClick={activate}
              onKeyDown={(event) => {
                if (isPlayable && (event.key === 'Enter' || event.key === ' ')) {
                  event.preventDefault();
                  activate();
                }
              }}
              onPointerDown={(event) => startHold(event, card)}
              onPointerMove={handleHoldPointerMove}
              onPointerUp={clearPendingHold}
              onPointerLeave={clearPendingHold}
              onPointerCancel={clearPendingHold}
              onContextMenu={(event) => event.preventDefault()}
              className="absolute bottom-0 left-1/2 shadow-lg transition-[left,transform,opacity] duration-200 ease-out"
              style={{
                height: `${cardHeight}px`,
                width: `${cardWidth}px`,
                touchAction: 'none',
                WebkitTouchCallout: 'none',
                userSelect: 'none',
                left: `calc(50% + ${offset * spacing}px)`,
                // Peek cards additionally shrink (PEEK_CARD_SCALE) on top of whatever scale they'd
                // already have — deliberately a *different* visual channel than the grayscale+dim
                // treatment below for an unplayable card, so "there's more this way" (small, faded,
                // full color) never reads as "this card can't be played" (full size, desaturated).
                transform: `translateX(-50%) translateY(${translateY}px) rotate(${rotation}deg) scale(${isPeeking ? scale * PEEK_CARD_SCALE : scale})`,
                zIndex: isHovered ? 100 : index,
                opacity: isWithinCarouselView ? 1 : isPeeking ? PEEK_CARD_OPACITY : 0,
                pointerEvents: isWithinCarouselView ? undefined : 'none',
                // Always pointer on hover, same convention as OpponentTile — hover is the "this is
                // interactive" affordance regardless of whether this particular card happens to be
                // playable right now. A card with no reason to report (not your turn) isn't "not
                // allowed" so much as "not applicable right now" — default cursor, not a slashed circle.
                cursor: isPlayable ? 'pointer' : hasUnplayableReason ? 'not-allowed' : 'default',
              }}
            >
              {/* InfoBadge is the sole explanation surface for an unplayable card — no `title=`
               * tooltip alongside it, to avoid the two drifting out of sync. */}
              <InfoBadge info={card.unplayableReason}>{cardImage}</InfoBadge>
            </div>
          );
        })}
      </div>
      {zoomedCard && (
        <CardZoomOverlay cardName={zoomedCard.name} onClose={() => setZoomedCard(null)} />
      )}
    </div>
  );
}

export default PlayerHand;
