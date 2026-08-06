import { useLayoutEffect, useMemo, useRef } from 'react';
import type { GameLogEntryView } from '../../api/types';
import { opponentColorForSeat } from '../../pages/GameBoard/assetMaps';

interface GameLogEntryListProps {
  entries: GameLogEntryView[];
}

/** How close to the physical top counts as "still following the latest" — a deliberate tolerance
 * band (not just subpixel rounding slack), so scrolling back to *near* the top re-arms pinning. */
const NEAR_TOP_THRESHOLD_PX = 4;

/**
 * Shared entry-list rendering, extracted so `GameLogPanel` (the desktop/tablet glass sidebar) and
 * `GameLogFocusPanel` (the phone-landscape solid floating panel) render the same log entries the
 * same way without duplicating this markup — the two containers differ only in chrome, not in how
 * an individual entry looks.
 *
 * `entries` arrives oldest-first (unchanged contract from `GameView.log`); this component renders
 * them newest-first in a normal top-down flex column so the newest entry sits at the physical top
 * with no CSS trickery. An earlier version relied on `flex-direction: column-reverse` to achieve
 * the same visual order without JS — that doesn't hold up: browsers' default resting scroll
 * position for a reversed, overflowing flex column lands at the *bottom* of the reversed content,
 * not the top, so an untouched log showed the oldest entries instead of the newest (see
 * game-log-feature.md §5, which documented the now-corrected CSS-only assumption).
 *
 * Scroll position is instead managed explicitly here, keyed off whether the user is currently
 * near the top of the list:
 * - Near the top → new entries keep them pinned there, so the latest message is always visible
 *   without scrolling.
 * - Scrolled away into history → new entries must not yank them back to the top. Their reading
 *   position is held visually steady (by compensating `scrollTop` for the height newly-inserted
 *   entries add above it) rather than merely left untouched, which would otherwise let the view
 *   drift toward older content as the list grows.
 * - Scrolling back to near the top at any point re-arms pinning for future entries, since "near
 *   the top" is read live off scroll position on every scroll event, not just on mount.
 *
 * `overflow-anchor: none` on the list turns off the browser's own native scroll anchoring, which
 * would otherwise ALSO try to compensate scroll position for content inserted above the viewport —
 * stacking on top of the explicit compensation below and overcorrecting.
 */
function GameLogEntryList({ entries }: GameLogEntryListProps) {
  const listRef = useRef<HTMLUListElement>(null);
  const isPinnedToTopRef = useRef(true);
  const previousScrollHeightRef = useRef(0);

  const newestFirstEntries = useMemo(() => [...entries].reverse(), [entries]);

  useLayoutEffect(() => {
    const list = listRef.current;
    if (!list) {
      return;
    }

    if (isPinnedToTopRef.current) {
      list.scrollTop = 0;
    } else {
      list.scrollTop += list.scrollHeight - previousScrollHeightRef.current;
    }
    previousScrollHeightRef.current = list.scrollHeight;
  }, [newestFirstEntries]);

  return (
    <ul
      ref={listRef}
      onScroll={(event) => {
        isPinnedToTopRef.current = event.currentTarget.scrollTop <= NEAR_TOP_THRESHOLD_PX;
      }}
      className="flex flex-1 flex-col gap-2.5 overflow-y-auto pr-1 min-h-0"
      style={{ overflowAnchor: 'none' }}
      aria-label="Game log"
      aria-live="polite"
    >
      {newestFirstEntries.map((entry) => (
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
