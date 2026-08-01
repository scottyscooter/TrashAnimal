import { useState } from 'react';
import type { HandCardView } from '../../api/types';
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
 * ranked-reason contract on `HandCardView`) rather than one flag for the whole fan. */
function PlayerHand({ handCards, onCardActivate }: PlayerHandProps) {
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);

  const count = handCards.length;
  const centerOffset = (count - 1) / 2;
  const isFanned = hoveredIndex !== null;
  const spacing = isFanned ? 177 : 90;
  const rotationStep = isFanned ? 4 : 2;
  const liftStep = isFanned ? 20 : 5;

  return (
    <div className="fixed bottom-[190px] left-1/2 z-10 h-[320px] w-[1050px] -translate-x-1/2">
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
            className="absolute bottom-0 left-1/2 h-[277px] w-[198px] shadow-lg transition-[left,transform] duration-200 ease-out"
            style={{
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
