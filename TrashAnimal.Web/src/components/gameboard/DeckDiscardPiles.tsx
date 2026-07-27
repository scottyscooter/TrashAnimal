import { useState } from 'react';
import type { DiscardCardView } from '../../api/types';
import { CARD_BACK_IMAGE, CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import DiscardCarouselModal from './DiscardCarouselModal';
import EmptyPileSlot from './EmptyPileSlot';

interface DeckDiscardPilesProps {
  deckCount: number;
  discardPile: DiscardCardView[];
}

function DeckDiscardPiles({ deckCount, discardPile }: DeckDiscardPilesProps) {
  const [carouselOpen, setCarouselOpen] = useState(false);
  const topDiscard = discardPile[discardPile.length - 1] ?? null;

  return (
    <>
      <div className="fixed left-1/2 top-[200px] z-10 flex -translate-x-1/2 items-end gap-16">
        <div className="flex flex-col items-center gap-2">
          <div className="relative h-[277px] w-[198px]">
            {[9, 4, 0].map((offset) => (
              <img
                key={offset}
                src={CARD_BACK_IMAGE}
                alt=""
                className="absolute rounded-[14px] object-cover shadow-lg"
                style={{ top: offset, left: offset, height: '100%', width: '100%' }}
              />
            ))}
            <span
              className="absolute -bottom-[15px] -right-[15px] flex h-11 w-11 items-center justify-center rounded-full border-2 text-sm font-bold"
              style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text-dark)' }}
            >
              {deckCount}
            </span>
          </div>
          <span
            className="text-[13px] font-semibold tracking-[0.08em]"
            style={{ color: 'var(--gb-text-label)', textShadow: '0 1px 2px rgba(0,0,0,.6)' }}
          >
            DECK
          </span>
        </div>

        <div className="flex flex-col items-center gap-2">
          <button
            type="button"
            onClick={() => setCarouselOpen(true)}
            disabled={!topDiscard}
            className="relative h-[277px] w-[198px] transition-transform duration-[180ms] hover:scale-[1.16] disabled:opacity-50 disabled:hover:scale-100"
          >
            {topDiscard ? (
              <img
                src={CARD_IMAGE_BY_NAME[topDiscard.name]}
                alt={topDiscard.name}
                className="h-full w-full rounded-[14px] object-cover shadow-lg"
              />
            ) : (
              <EmptyPileSlot className="h-full w-full rounded-[14px]" />
            )}
            <span
              className="absolute -bottom-[15px] -right-[15px] flex h-11 w-11 items-center justify-center rounded-full border-2 text-sm font-bold"
              style={{ background: 'var(--gb-red)', color: 'var(--gb-gold-text-dark)' }}
            >
              {discardPile.length}
            </span>
          </button>
          <span
            className="text-[13px] font-semibold tracking-[0.08em]"
            style={{ color: 'var(--gb-text-label)', textShadow: '0 1px 2px rgba(0,0,0,.6)' }}
          >
            DISCARD
          </span>
        </div>
      </div>

      {carouselOpen && (
        <DiscardCarouselModal discardPile={discardPile} onClose={() => setCarouselOpen(false)} />
      )}
    </>
  );
}

export default DeckDiscardPiles;
