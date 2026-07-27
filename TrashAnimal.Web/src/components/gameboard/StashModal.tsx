import type { StashableHandCard } from '../../api/types';
import { CARD_IMAGE_BY_NAME } from '../../pages/GameBoard/assetMaps';
import Modal from './Modal';

interface StashModalProps {
  title: string;
  faceUpCards: StashableHandCard[];
  onClose: () => void;
}

/** Groups face-up stash cards by CardName with a count badge, sorted highest-count-first —
 * shared by the player's own face-up stash modal and the opponent-detail modal's stash section. */
function StashModal({ title, faceUpCards, onClose }: StashModalProps) {
  const grouped = new Map<string, number>();
  for (const card of faceUpCards) {
    grouped.set(card.name, (grouped.get(card.name) ?? 0) + 1);
  }
  const entries = [...grouped.entries()].sort((a, b) => b[1] - a[1]);

  return (
    <Modal onClose={onClose} labelledBy="stash-modal-heading" wide>
      <h2 id="stash-modal-heading" className="mb-1 text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
        {title}
      </h2>
      <p className="mb-4 text-xs tracking-[0.06em]" style={{ color: 'var(--gb-text-label)' }}>
        {faceUpCards.length} TOTAL
      </p>
      <div className="flex flex-wrap gap-3">
        {entries.map(([name, count]) => (
          <div key={name} className="relative">
            <img
              src={CARD_IMAGE_BY_NAME[name as keyof typeof CARD_IMAGE_BY_NAME]}
              alt={name}
              className="h-[140px] w-[100px] rounded-lg object-cover"
            />
            <span
              className="absolute -bottom-2 -right-2 flex h-7 w-7 items-center justify-center rounded-full border-2 text-xs font-bold"
              style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)', borderColor: 'var(--gb-gold-text-dark)' }}
            >
              {count}
            </span>
          </div>
        ))}
        {entries.length === 0 && <p style={{ color: 'var(--gb-text-label)' }}>Nothing here yet.</p>}
      </div>
    </Modal>
  );
}

export default StashModal;
