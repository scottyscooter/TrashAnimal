import type { GameLogEntryView } from '../../api/types';
import { opponentColorForSeat } from '../../pages/GameBoard/assetMaps';

interface GameLogPanelProps {
  entries: GameLogEntryView[];
}

/**
 * Renders `entries` oldest-first inside a `column-reverse` scroll container, so the newest
 * entry appears visually at the top without any client-side re-sorting as new entries arrive
 * (see game-log-feature.md §5). Styled to match the high-fidelity mockup at
 * .claude/docs/plans/design_handoff_main_game_view/mainView_desktop.html — all values are pixel-close.
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
      <ul className="flex flex-1 flex-col-reverse gap-2.5 overflow-y-auto pr-1 min-h-0" aria-label="Game log" aria-live="polite">
        {entries.map((entry) => (
          <li key={entry.sequenceNumber} className="flex gap-2 items-start">
            <span
              className="h-2 w-2 shrink-0 rounded-full"
              style={{
                background: opponentColorForSeat(entry.actingPlayerSeat),
                marginTop: '5px',
              }}
              aria-hidden="true"
            />
            <div className="flex flex-col gap-0">
              <span className="text-[13px] leading-[1.4]" style={{ color: 'var(--gb-text-log)' }}>
                {entry.message}
              </span>
              <span className="text-[11px]" style={{ color: 'var(--gb-text-timestamp)' }}>
                Turn {entry.turnNumber}
              </span>
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default GameLogPanel;
