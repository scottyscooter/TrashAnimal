import { useEffect } from 'react';
import type { MouseEvent } from 'react';
import { createPortal } from 'react-dom';
import type { CardName } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';

interface CardZoomOverlayProps {
  cardName: CardName;
  onClose: () => void;
}

/** Full-screen, non-interactive preview of a single card, opened by holding a card in
 * `PlayerHand`. Unlike `Modal`, there's nothing actionable inside to protect from an
 * inside-click closing it — the entire scrim (including the area behind the card image)
 * closes the overlay on click, same as pressing the explicit close button or Escape. */
function CardZoomOverlay({ cardName, onClose }: CardZoomOverlayProps) {
  useEffect(() => {
    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  function handleCloseButtonClick(event: MouseEvent) {
    event.stopPropagation();
    onClose();
  }

  return createPortal(
    <div
      role="dialog"
      aria-modal="true"
      aria-label={`${cardName} card, enlarged`}
      onClick={onClose}
      className="fixed inset-0 z-50 flex items-center justify-center p-6"
      style={{ background: 'rgba(5,10,20,.78)', backdropFilter: 'blur(3px)' }}
    >
      <div className="relative">
        <img
          src={CARD_IMAGE_BY_NAME[cardName]}
          alt={cardName}
          className="max-h-[70vh] w-auto rounded-[18px] shadow-2xl"
        />
        <button
          type="button"
          onClick={handleCloseButtonClick}
          aria-label="Close"
          className="absolute -right-3 -top-3 flex h-8 w-8 items-center justify-center rounded-full text-lg"
          style={{
            background: 'rgba(18,26,46,.95)',
            border: '1px solid rgba(255,255,255,.25)',
            color: 'var(--gb-text-primary)',
          }}
        >
          ✕
        </button>
      </div>
    </div>,
    document.body,
  );
}

export default CardZoomOverlay;
