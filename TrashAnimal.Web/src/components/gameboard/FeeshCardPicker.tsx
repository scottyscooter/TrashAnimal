import type { DiscardCardView } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import Modal from './Modal';

interface FeeshCardPickerProps {
  discardPile: DiscardCardView[];
  onPick: (cardId: string) => void;
  onClose: () => void;
  isPending: boolean;
}

/** Reusable card-picker over the discard pile, used to complete Feesh. */
function FeeshCardPicker({ discardPile, onPick, onClose, isPending }: FeeshCardPickerProps) {
  return (
    <Modal onClose={onClose} labelledBy="feesh-picker-heading" wide>
      <h2 id="feesh-picker-heading" className="mb-4 text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
        Pick a card from the discard pile
      </h2>
      <div className="flex flex-wrap gap-3">
        {discardPile.map((card) => (
          <button
            key={card.cardId}
            type="button"
            disabled={isPending}
            onClick={() => onPick(card.cardId)}
            className="flex flex-col items-center gap-1 rounded-xl border-2 border-transparent p-1 transition-transform hover:scale-[1.1] disabled:opacity-50"
          >
            <img
              src={CARD_IMAGE_BY_NAME[card.name]}
              alt={card.name}
              className="h-[112px] w-20 rounded-lg object-cover"
            />
            <span className="text-xs" style={{ color: 'var(--gb-text-label)' }}>
              {card.name}
            </span>
          </button>
        ))}
        {discardPile.length === 0 && (
          <p style={{ color: 'var(--gb-text-label)' }}>The discard pile is empty.</p>
        )}
      </div>
    </Modal>
  );
}

export default FeeshCardPicker;
