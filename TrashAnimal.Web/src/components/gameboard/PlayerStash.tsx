import { useState } from 'react';
import type { OwnStashView } from '../../api/types';
import { CARD_BACK_IMAGE, CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import EmptyPileSlot from './EmptyPileSlot';
import StashModal from './StashModal';

interface PlayerStashProps {
  ownStash: OwnStashView;
}

function PlayerStash({ ownStash }: PlayerStashProps) {
  const [openModal, setOpenModal] = useState<'faceDown' | 'faceUp' | null>(null);
  const total = ownStash.faceDownCards.length + ownStash.faceUpCards.length;
  const topFaceUp = ownStash.faceUpCards[ownStash.faceUpCards.length - 1] ?? null;

  return (
    <>
      <div className="fixed bottom-16 left-[60px] z-10 flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="text-xs font-semibold tracking-[0.12em]" style={{ color: 'var(--gb-text-label)' }}>
            YOUR STASH
          </span>
          <span
            className="rounded-full border px-2 py-0.5 text-[11px] font-bold"
            style={{ background: 'rgba(8,14,28,.7)', borderColor: 'rgba(255,255,255,.25)', color: 'var(--gb-text-primary)' }}
          >
            {total} TOTAL
          </span>
        </div>
        <div className="flex gap-5">
          <button
            type="button"
            onClick={() => setOpenModal('faceDown')}
            disabled={ownStash.faceDownCards.length === 0}
            className="relative h-[180px] w-[128px] transition-transform duration-[180ms] hover:scale-[1.16] disabled:opacity-50 disabled:hover:scale-100"
          >
            {ownStash.faceDownCards.length > 0 ? (
              <img src={CARD_BACK_IMAGE} alt="Face-down stash" className="h-full w-full rounded-[10px] object-cover" />
            ) : (
              <EmptyPileSlot className="h-full w-full rounded-[10px]" />
            )}
            <span
              className="absolute -bottom-2 -right-2 flex h-9 w-9 items-center justify-center rounded-full border-2 text-xs font-bold"
              style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text-dark)' }}
            >
              {ownStash.faceDownCards.length}
            </span>
          </button>
          <button
            type="button"
            onClick={() => setOpenModal('faceUp')}
            disabled={!topFaceUp}
            className="relative h-[180px] w-[128px] transition-transform duration-[180ms] hover:scale-[1.16] disabled:opacity-50 disabled:hover:scale-100"
          >
            {topFaceUp ? (
              <img
                src={CARD_IMAGE_BY_NAME[topFaceUp.name]}
                alt={topFaceUp.name}
                className="h-full w-full rounded-[10px] object-cover"
              />
            ) : (
              <EmptyPileSlot className="h-full w-full rounded-[10px]" />
            )}
            <span
              className="absolute -bottom-2 -right-2 flex h-9 w-9 items-center justify-center rounded-full border-2 text-xs font-bold"
              style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
            >
              {ownStash.faceUpCards.length}
            </span>
          </button>
        </div>
      </div>

      {openModal === 'faceDown' && (
        <StashModal
          title="Your Face-Down Stash"
          cards={ownStash.faceDownCards}
          onClose={() => setOpenModal(null)}
        />
      )}
      {openModal === 'faceUp' && (
        <StashModal
          title="Your Face-Up Stash"
          cards={ownStash.faceUpCards}
          onClose={() => setOpenModal(null)}
        />
      )}
    </>
  );
}

export default PlayerStash;
