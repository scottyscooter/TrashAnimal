import type { OpponentSummaryView } from '../../api/types';
import Modal from './Modal';

interface VictimPickerProps {
  title: string;
  opponents: OpponentSummaryView[];
  onPick: (victimSeat: number) => void;
  onClose: () => void;
  isPending: boolean;
}

/** Reusable seat-picker over gameView.opponents (Shiny's victimSeat and the Steal token's
 * victimSeat both need one). */
function VictimPicker({ title, opponents, onPick, onClose, isPending }: VictimPickerProps) {
  return (
    <Modal onClose={onClose} labelledBy="victim-picker-heading">
      <h2 id="victim-picker-heading" className="mb-4 text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
        {title}
      </h2>
      <div className="flex flex-col gap-2">
        {opponents.map((opponent) => (
          <button
            key={opponent.seatIndex}
            type="button"
            disabled={isPending}
            onClick={() => onPick(opponent.seatIndex)}
            className="gb-glass gb-glass-hover rounded-lg px-4 py-3 text-left text-sm font-medium disabled:opacity-50"
            style={{ color: 'var(--gb-text-primary)' }}
          >
            {opponent.name}
            <span className="ml-2 text-xs" style={{ color: 'var(--gb-text-label)' }}>
              ({opponent.handCount} in hand)
            </span>
          </button>
        ))}
      </div>
    </Modal>
  );
}

export default VictimPicker;
