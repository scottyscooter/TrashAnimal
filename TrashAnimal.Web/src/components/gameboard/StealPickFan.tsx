import { CARD_IMAGE_BY_NAME, CARD_BACK_IMAGE } from '../../pages/GameBoard/assetMaps';
import type { StealPickSlot } from '../../api/types';
import { STEAL_PICK_SLOT_UNREVEALED_LABEL } from '../../api/types';

interface StealPickFanProps {
  slots: StealPickSlot[];
  onPick: (cardId: string) => void;
  isPending: boolean;
}

/** Renders steal-pick slots as an unordered, flat horizontal fan of card tiles.
 * Unrevealed hand/stash cards display card-back images; revealed face-up stash cards
 * display their actual card images. Fixed tile sizing ensures position conveys no information. */
function StealPickFan({ slots, onPick, isPending }: StealPickFanProps) {
  return (
    <div className="flex flex-wrap gap-3">
      {slots.map((slot) => {
        const isUnrevealed = slot.thiefFacingLabel === STEAL_PICK_SLOT_UNREVEALED_LABEL;
        const imageSrc = isUnrevealed
          ? CARD_BACK_IMAGE
          : CARD_IMAGE_BY_NAME[slot.thiefFacingLabel as keyof typeof CARD_IMAGE_BY_NAME] ?? CARD_BACK_IMAGE;
        const altText = isUnrevealed ? 'Unrevealed card' : slot.thiefFacingLabel;

        return (
          <button
            key={slot.cardId}
            type="button"
            disabled={isPending}
            onClick={() => onPick(slot.cardId)}
            className="rounded-lg border-2 border-transparent transition-opacity disabled:opacity-50"
            style={{ padding: '4px' }}
          >
            <img
              src={imageSrc}
              alt={altText}
              className="h-[112px] w-20 rounded-lg object-cover"
            />
          </button>
        );
      })}
    </div>
  );
}

export default StealPickFan;
