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
  // Phone landscape gets its own, much smaller dome step than desktop's fanned 20px: the dome
  // effect (translateY below) drops off-center cards by offset * liftStep past the fan's resting
  // line, and desktop has room to spare (277px cards, overflow: visible) to absorb that. Phone
  // cards are only 140px tall inside a scroll layer that CANNOT show unclipped overflow (see
  // domeCompensation below) — 20px/card-of-offset compounds into 60px+ for a normal-size hand,
  // which is both proportionally huge on a 140px card and forces an equally large compensating
  // shift. 8px keeps the same visual "dome" read at a scale the compensation stays unobtrusive at.
  const liftStep = isPhoneLandscape ? 8 : isFanned ? 20 : 5;

  const cardWidth = isPhoneLandscape ? 100 : 198;
  const cardHeight = isPhoneLandscape ? 140 : 277;

  // Every card is anchored bottom:0 within the scroll layer below, then the dome effect (see
  // translateY) pushes cards further from center DOWN by offset * liftStep — past that same
  // bottom edge, since bottom:0 already sits flush with it before any translate is applied. The
  // scroll layer's own height must already contain the full dome to avoid clipping the deepest
  // cards (overflow-x-auto forces overflow-y to compute as auto too, so an under-sized box clips
  // vertically instead of just failing to scroll) — but just making the layer taller doesn't
  // achieve that on its own: cards keep the SAME bottom:0 anchor regardless of the layer's height,
  // so the deepest card's rendered position relative to that anchor doesn't change. Reaching zero
  // clipping while keeping the fan's resting line in the same place requires shifting the whole
  // dome up by its own max drop (domeCompensation, subtracted from translateY below) so the
  // deepest card's bottom lands exactly on the layer's bottom edge instead of past it — which
  // necessarily moves the CENTER card up by the same amount from where it sat pre-fix. Phone's
  // much smaller liftStep (above) is what keeps that shift small enough not to read as the fan
  // relocating, rather than eliminating it outright, which isn't possible with a downward-opening
  // dome anchored to a fixed bottom edge.
  const maxDomeDrop = centerOffset * liftStep;
  const domeCompensation = isPhoneLandscape ? maxDomeDrop : 0;
  const phoneHandScrollHeight = cardHeight + maxDomeDrop + 8;

  return (
    <div className="fixed bottom-[190px] left-1/2 z-10 h-[320px] w-[1050px] -translate-x-1/2 phone-landscape:bottom-auto phone-landscape:top-1/2 phone-landscape:-translate-y-1/2 phone-landscape:h-[220px] phone-landscape:w-[90%] phone-landscape:max-w-[900px]">
      {/* Horizontal-scroll layer (phone landscape only — desktop just fills this 1:1, no
          scrolling, same box it always was). overflow-x-auto forces overflow-y to compute as auto
          too (setting either axis to non-visible does), so this layer's own height must already
          contain the full dome — see phoneHandScrollHeight above — since anything taller would
          get vertically clipped by this same rule, not just left un-scrollable. */}
      <div
        className="absolute inset-x-0 bottom-0 phone-landscape:overflow-x-auto"
        style={{
          height: isPhoneLandscape ? `${phoneHandScrollHeight}px` : '100%',
          touchAction: isPhoneLandscape ? 'pan-x' : undefined,
        }}
      >
        {handCards.map((card, index) => {
          const offset = index - centerOffset;
          const isHovered = hoveredIndex === index;
          const rotation = offset * rotationStep;
          // Downward-opening arc (dome): the center card sits highest, cards drop further down the
          // further they are from center. Hovering lifts that one card up an additional 34px.
          const dropFromCenter = Math.abs(offset) * liftStep;
          const hoverLift = isHovered ? 34 : 0;
          const translateY = dropFromCenter - domeCompensation - hoverLift;
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
    </div>
  );
}

export default PlayerHand;
