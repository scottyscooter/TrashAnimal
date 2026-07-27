import type { CardName, StashableHandCard } from '../../api/types';
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
          style={{ color: 'var(--gb-text-primary)' }}
        >
          Pass
        </button>
      </div>
    </Modal>
  );
}

export default BanditResponseModal;
