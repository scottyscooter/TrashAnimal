import type { GameLogEntryView } from '../../api/types';
import { opponentColorForSeat } from '../../pages/GameBoard/assetMaps';

interface GameLogEntryListProps {
  entries: GameLogEntryView[];
}

/**
 * Shared entry-list rendering, extracted so `GameLogPanel` (the desktop/tablet glass sidebar) and
 * `GameLogFocusPanel` (the phone-landscape solid floating panel) render the same log entries the
 * same way without duplicating this markup — the two containers differ only in chrome, not in how
 * an individual entry looks. Renders `entries` oldest-first inside a `column-reverse` scroll
 * container, so the newest entry appears visually at the top without any client-side re-sorting as
 * new entries arrive (see game-log-feature.md §5).
 */
function GameLogEntryList({ entries }: GameLogEntryListProps) {
  return (
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
  );
}

export default GameLogEntryList;
