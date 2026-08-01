import type { GameLogEntryView } from '../../api/types';
import GameLogEntryList from './GameLogEntryList';

interface GameLogFocusPanelProps {
  entries: GameLogEntryView[];
  onClose: () => void;
}

/**
 * Phone-landscape-only game log focus panel — a distinct component from `GameLogPanel`, not a
 * resized variant of it. `GameLogPanel` is an always-visible glass sidebar flush against the
 * board; this is a dismissible floating panel that takes over input focus while open (see the
 * full-screen click-catcher rendered alongside it in `GameBoardPage.tsx`).
 *
 * Floats with margin on top/right/bottom (~5% inset, never flush to those three edges) and stays
 * anchored to roughly the same left-edge position as a flush sidebar (~28% width). Background is
 * solid (~95% opacity, no backdrop blur on the panel itself) so it reads as the sharp foreground
 * surface against the blurred/locked board behind it — the blur lives on `GameBoardPage`'s
 * background wrapper, not on this panel.
 */
function GameLogFocusPanel({ entries, onClose }: GameLogFocusPanelProps) {
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="game-log-focus-heading"
      className="fixed top-[5%] right-[4%] bottom-[5%] z-40 hidden w-[28%] flex-col rounded-2xl p-4 phone-landscape:flex"
      style={{
        background: 'rgba(18,26,46,.95)',
        border: '1px solid var(--gb-glass-border)',
        boxShadow: 'var(--gb-modal-shadow)',
      }}
    >
      <div className="mb-2.5 flex items-center justify-between">
        <span
          id="game-log-focus-heading"
          className="text-[13px] font-semibold tracking-[0.12em]"
          style={{ color: 'var(--gb-text-label)' }}
        >
          GAME LOG
        </span>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close game log"
          className="text-lg"
          style={{ color: 'var(--gb-text-label)' }}
        >
          ✕
        </button>
      </div>
      <GameLogEntryList entries={entries} />
    </div>
  );
}

export default GameLogFocusPanel;
