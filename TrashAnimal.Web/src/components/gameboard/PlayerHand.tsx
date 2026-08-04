import { useState } from 'react';
import type { HandCardView } from '../../api/types';
import { useIsPhoneLandscape } from '../../hooks/useLandscapeBreakpoint';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import InfoBadge from '../InfoBadge';

interface PlayerHandProps {
  handCards: HandCardView[];
  /** Called with the card's own `playableAs` action when a playable card is activated (click or
   * Enter/Space). Never called for a card whose `playableAs` is null — the caller does not need to
   * re-check playability. Routing the action to the right handler (e.g. opening the Feesh discard
   * picker vs. the Shiny victim picker vs. dispatching a plain token-phase action) is the caller's
   * job, same as every other action-dispatch entry point in GameBoardPage. */
  onCardActivate: (card: HandCardView) => void;
}

/** Fanned hand per the design: hover-spreads the whole fan, and the specifically-hovered card
 * additionally scales up and lifts to the front.
 *
 * Playability is per-card (`card.playableAs`/`card.unplayableReason`, driven by the backend's
 * ranked-reason contract on `HandCardView`) rather than one flag for the whole fan.
 *
 * On phone landscape, `isFanned` is forced permanently true instead of hover-driven: touch devices
 * never fire `mouseenter`/`mouseleave`, so the hover-gated "tight" resting state (meant only to
 * save space before a hover reveal) would otherwise be the *only* state a phone ever renders,
 * leaving cards packed 95px+ into each other with no way to reach an individual one. See the
 * mobile landscape plan, Round 2 Finding 2. */
function PlayerHand({ handCards, onCardActivate }: PlayerHandProps) {
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);

  const count = handCards.length;
  const centerOffset = (count - 1) / 2;
  const isPhoneLandscape = useIsPhoneLandscape();
  const isFanned = isPhoneLandscape || hoveredIndex !== null;

  // Phone-landscape spacing/card size are smaller than desktop's "fanned" values, not just its
  // "resting" ones — desktop's 177px fanned spacing assumes a 198px-wide card with room to spare;
  // phone landscape needs every card visible within a ~900px budget without a hover gesture, so it
  // gets its own compact-but-always-spread scale instead of borrowing the desktop hover values.
  const spacing = isPhoneLandscape ? 62 : isFanned ? 177 : 90;
  const rotationStep = isFanned ? 4 : 2;
  const liftStep = isFanned ? 20 : 5;

  const cardWidth = isPhoneLandscape ? 100 : 198;
  const cardHeight = isPhoneLandscape ? 140 : 277;

  return (
    // phone-landscape:h-[220px] (not h-auto): the container's own box needs an explicit height to
    // (a) center predictably via top-1/2/-translate-y-1/2 (an absolutely-positioned-only child set
    // contributes nothing to an auto height) and (b) safely contain the fanned cards' rotation/lift
    // range (140px card height + up to ~50px of lift/drop) once overflow-x-auto is added below —
    // setting overflow-x to anything but visible forces the browser to treat overflow-y as auto too,
    // so an under-sized box would silently clip cards vertically rather than just scroll horizontally.
    <div
      className="fixed bottom-[190px] left-1/2 z-10 h-[320px] w-[1050px] -translate-x-1/2 phone-landscape:bottom-auto phone-landscape:top-1/2 phone-landscape:-translate-y-1/2 phone-landscape:h-[220px] phone-landscape:w-[90%] phone-landscape:max-w-[900px] phone-landscape:overflow-x-auto"
      style={{ touchAction: isPhoneLandscape ? 'pan-x' : undefined }}
    >
      {handCards.map((card, index) => {
        const offset = index - centerOffset;
        const isHovered = hoveredIndex === index;
        const rotation = offset * rotationStep;
        // Downward-opening arc (dome): the center card sits highest, cards drop further down the
        // further they are from center. Hovering lifts that one card up an additional 34px.
        const dropFromCenter = Math.abs(offset) * liftStep;
        const hoverLift = isHovered ? 34 : 0;
        const translateY = dropFromCenter - hoverLift;
        const scale = isHovered ? 1.16 : 1;
        const isPlayable = card.playableAs !== null;
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
            tabIndex={isPlayable ? 0 : -1}
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
            className="absolute bottom-0 left-1/2 shadow-lg transition-[left,transform] duration-200 ease-out"
            style={{
              height: `${cardHeight}px`,
              width: `${cardWidth}px`,
              left: `calc(50% + ${offset * spacing}px)`,
              transform: `translateX(-50%) translateY(${translateY}px) rotate(${rotation}deg) scale(${scale})`,
              zIndex: isHovered ? 100 : index,
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
  );
}

export default PlayerHand;
