import { useState } from 'react';
import type { GameAction, HandCardView } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';

interface PlayerHandProps {
  handCards: HandCardView[];
  allowedActions: GameAction[];
  onFeeshClick: () => void;
}

/** Fanned hand per the design: hover-spreads the whole fan, and the specifically-hovered card
 * additionally scales up and lifts to the front. */
function PlayerHand({ handCards, allowedActions, onFeeshClick }: PlayerHandProps) {
  const [hoveredIndex, setHoveredIndex] = useState<number | null>(null);
  const canPlayFeesh = allowedActions.includes('PlayFeesh');

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

        return (
          // A native <button disabled> suppresses hover/mouseenter in most browsers, not just
          // click — since Feesh is only playable in narrow circumstances, that made the whole fan
          // hover effect dead almost all the time. Hover must stay live regardless of click-ability.
          <div
            key={card.cardId}
            role="button"
            tabIndex={canPlayFeesh ? 0 : -1}
            aria-disabled={!canPlayFeesh}
            onMouseEnter={() => setHoveredIndex(index)}
            onMouseLeave={() => setHoveredIndex((current) => (current === index ? null : current))}
            onClick={() => canPlayFeesh && onFeeshClick()}
            onKeyDown={(event) => {
              if (canPlayFeesh && (event.key === 'Enter' || event.key === ' ')) {
                event.preventDefault();
                onFeeshClick();
              }
            }}
            className="absolute bottom-0 left-1/2 h-[277px] w-[198px] overflow-hidden rounded-[14px] shadow-lg transition-[left,transform] duration-200 ease-out"
            style={{
              left: `calc(50% + ${offset * spacing}px)`,
              transform: `translateX(-50%) translateY(${translateY}px) rotate(${rotation}deg) scale(${scale})`,
              zIndex: isHovered ? 100 : index,
              // Always pointer on hover, same convention as OpponentTile — hover is the "this is
              // interactive" affordance regardless of whether Feesh happens to be playable right now.
              cursor: 'pointer',
            }}
            title={canPlayFeesh ? `Play Feesh to retrieve a card (uses ${card.name})` : card.name}
          >
            <img
              src={CARD_IMAGE_BY_NAME[card.name]}
              alt={card.name}
              className="h-full w-full object-cover"
            />
          </div>
        );
      })}
    </div>
  );
}

export default PlayerHand;
