import { useState } from 'react';
import type { DiscardCardView } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import Modal from './Modal';

interface DiscardCarouselModalProps {
  discardPile: DiscardCardView[];
  onClose: () => void;
}

/** 3-card carousel: center card full size, neighbors smaller/faded, matching the design spec. */
function DiscardCarouselModal({ discardPile, onClose }: DiscardCarouselModalProps) {
  const [index, setIndex] = useState(discardPile.length - 1);
  const total = discardPile.length;

  if (total === 0) {
    return (
      <Modal onClose={onClose} labelledBy="discard-carousel-heading">
        <h2 id="discard-carousel-heading" className="text-center text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
          DISCARD PILE
        </h2>
        <p className="mt-2 text-center" style={{ color: 'var(--gb-text-label)' }}>
          Nothing here yet.
        </p>
      </Modal>
    );
  }

  const prev = discardPile[index - 1] ?? null;
  const current = discardPile[index];
  const next = discardPile[index + 1] ?? null;

  return (
    <Modal onClose={onClose} labelledBy="discard-carousel-heading" wide fitContent>
      <h2
        id="discard-carousel-heading"
        className="mb-4 text-center text-lg font-semibold phone-landscape:mb-1 phone-landscape:text-base"
        style={{ color: 'var(--gb-text-primary)' }}
      >
        DISCARD PILE
      </h2>
      <div className="flex items-center justify-center gap-4 phone-landscape:gap-2">
        <button
          type="button"
          onClick={() => setIndex((i) => Math.max(0, i - 1))}
          disabled={index === 0}
          aria-label="Previous card"
          className="gb-glass flex h-[52px] w-[52px] items-center justify-center rounded-full text-xl disabled:opacity-30 phone-landscape:h-[36px] phone-landscape:w-[36px]"
          style={{ color: 'var(--gb-text-primary)' }}
        >
          ‹
        </button>

        <div className="flex items-center gap-4 phone-landscape:gap-2">
          {prev ? (
            <img
              src={CARD_IMAGE_BY_NAME[prev.name]}
              alt={prev.name}
              className="h-[238px] w-[170px] rounded-lg object-cover opacity-55 transition-all duration-[250ms] phone-landscape:h-[104px] phone-landscape:w-[74px]"
              style={{ transform: 'scale(0.9)' }}
            />
          ) : (
            <div
              aria-hidden="true"
              className="h-[238px] w-[170px] phone-landscape:h-[104px] phone-landscape:w-[74px]"
            />
          )}
          <img
            src={CARD_IMAGE_BY_NAME[current.name]}
            alt={current.name}
            className="h-[364px] w-[260px] rounded-lg object-cover shadow-2xl transition-all duration-[250ms] phone-landscape:h-[160px] phone-landscape:w-[114px]"
          />
          {next ? (
            <img
              src={CARD_IMAGE_BY_NAME[next.name]}
              alt={next.name}
              className="h-[238px] w-[170px] rounded-lg object-cover opacity-55 transition-all duration-[250ms] phone-landscape:h-[104px] phone-landscape:w-[74px]"
              style={{ transform: 'scale(0.9)' }}
            />
          ) : (
            <div
              aria-hidden="true"
              className="h-[238px] w-[170px] phone-landscape:h-[104px] phone-landscape:w-[74px]"
            />
          )}
        </div>

        <button
          type="button"
          onClick={() => setIndex((i) => Math.min(total - 1, i + 1))}
          disabled={index === total - 1}
          aria-label="Next card"
          className="gb-glass flex h-[52px] w-[52px] items-center justify-center rounded-full text-xl disabled:opacity-30 phone-landscape:h-[36px] phone-landscape:w-[36px]"
          style={{ color: 'var(--gb-text-primary)' }}
        >
          ›
        </button>
      </div>
      <p className="mt-4 text-center text-sm phone-landscape:mt-1 phone-landscape:text-xs" style={{ color: 'var(--gb-text-label)' }}>
        {index + 1} / {total}
      </p>
    </Modal>
  );
}

export default DiscardCarouselModal;
