import type { CardName, StashableHandCard } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import CardCountBadge from './CardCountBadge';
import Modal from './Modal';

interface BanditResponseModalProps {
  revealedCardName: CardName;
  stashableCards: StashableHandCard[];
  onStash: (cardId: string) => void;
  onPass: () => void;
  isPending: boolean;
}

/** Shown to whichever opponent is currently the Bandit responder — independent of whose turn it
 * is, since the responder is never the active player. Only one matching card can be stashed per
 * response; if the responder happens to hold more than one match, the first is used. */
function BanditResponseModal({
  revealedCardName,
  stashableCards,
  onStash,
  onPass,
  isPending,
}: BanditResponseModalProps) {
  const canStash = stashableCards.length > 0;

  return (
    <Modal onClose={() => {}} labelledBy="bandit-response-heading">
      <h2 id="bandit-response-heading" className="mb-4 text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
        Would you like to stash a {revealedCardName} face-up or pass?
      </h2>      
      <div className="mx-auto mb-4 w-fit phone-landscape:mb-2">
        <CardCountBadge count={stashableCards.length} size="medium" includeResponsive={false}>
          <img
            src={CARD_IMAGE_BY_NAME[revealedCardName]}
            alt={revealedCardName}
            className="h-[168px] w-[120px] rounded-lg object-cover phone-landscape:h-[110px] phone-landscape:w-[78px]"
          />
        </CardCountBadge>
      </div>
      <div className="flex gap-3">
        <button
          type="button"
          disabled={!canStash || isPending}
          onClick={() => canStash && onStash(stashableCards[0].cardId)}
          className="flex-1 rounded-lg py-2 text-sm font-bold disabled:opacity-50"
          style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
        >
          Stash
        </button>
        <button
          type="button"
          disabled={isPending}
          onClick={onPass}
          className="gb-glass flex-1 rounded-lg py-2 text-sm font-bold disabled:opacity-50"
          style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
        >
          Pass
        </button>
      </div>
    </Modal>
  );
}

export default BanditResponseModal;
