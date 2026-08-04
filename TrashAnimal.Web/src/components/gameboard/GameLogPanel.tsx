import type { GameLogEntryView } from '../../api/types';
import GameLogEntryList from './GameLogEntryList';

interface GameLogPanelProps {
  entries: GameLogEntryView[];
}

/**
 * Desktop/tablet-landscape glass sidebar for the game log — always-visible, sits flush against the
 * board's other glass panels. Styled to match the high-fidelity mockup at
 * .claude/docs/plans/design_handoff_main_game_view/mainView_desktop.html — all values are pixel-close.
 * Hidden on phone landscape (see its `GameBoardPage.tsx` wrapper's `phone-landscape:hidden`) in
 * favor of `GameLogFocusPanel`, a distinct floating/solid-background panel with its own chrome.
 */
function GameLogPanel({ entries }: GameLogPanelProps) {
  return (
    <div
      className="flex h-full flex-col rounded-2xl p-3.5"
      style={{
        background: 'var(--gb-glass-bg)',
        backdropFilter: 'blur(6px)',
        border: '1px solid var(--gb-glass-border)',
        boxShadow: 'var(--gb-glass-shadow)',
      }}
    >
      <span
        className="mb-2.5 text-[13px] font-semibold tracking-[0.12em]"
        style={{ color: 'var(--gb-text-label)' }}
      >
        GAME LOG
      </span>
      <GameLogEntryList entries={entries} />
    </div>
  );
}

export default GameLogPanel;
