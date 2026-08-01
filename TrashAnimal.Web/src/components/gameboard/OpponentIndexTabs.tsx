import { useState } from 'react';
import type { GameView } from '../../api/types';
import { opponentColorForSeat } from '../../pages/GameBoard/assetMaps';
import OpponentDetailModal from './OpponentDetailModal';

interface OpponentIndexTabsProps {
  gameView: GameView;
}

/**
 * Phone-landscape-only replacement trigger for `OpponentRail`'s always-visible tiles: one small
 * tab per opponent stacked down the left edge. Tapping any tab opens the same `OpponentDetailModal`
 * used by `OpponentRail`, targeted at that opponent's index — this component owns no modal content
 * of its own, only the trigger + "which opponent is selected" state.
 *
 * Hidden via `hidden phone-landscape:flex` (CSS-only) rather than `useIsPhoneLandscape()` — nothing
 * here needs JS-level orientation awareness, only visibility, so plain Tailwind variants suffice.
 */
function OpponentIndexTabs({ gameView }: OpponentIndexTabsProps) {
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);

  const { opponents, currentPlayerIndex } = gameView;

  // "Contextually active" opponent: whoever's turn it currently is, if that's an opponent. If it's
  // the local player's turn instead, `currentPlayerIndex` won't match any opponent's seatIndex
  // (findIndex returns -1), so this naturally falls back to the first opponent in turn order —
  // exactly the rule from the plan, with no need for a separate "is it my turn" input.
  const activeOpponentIndex = opponents.findIndex((opponent) => opponent.seatIndex === currentPlayerIndex);
  const contextuallyActiveIndex = activeOpponentIndex === -1 ? 0 : activeOpponentIndex;

  return (
    <>
      <div
        className="fixed left-0 top-1/2 z-10 hidden -translate-y-1/2 flex-col gap-1.5 phone-landscape:flex"
        aria-label="Opponents"
      >
        {opponents.map((opponent, index) => {
          const isContextuallyActive = index === contextuallyActiveIndex;

          return (
            <button
              key={opponent.seatIndex}
              type="button"
              onClick={() => setSelectedIndex(index)}
              aria-label={`View ${opponent.name}`}
              className="flex h-8 w-8 shrink-0 items-center justify-center rounded-r-lg border text-xs font-bold transition-transform hover:scale-105"
              style={{
                background: isContextuallyActive ? 'var(--gb-gold)' : opponentColorForSeat(opponent.seatIndex),
                color: isContextuallyActive ? 'var(--gb-gold-text)' : 'var(--gb-text-on-avatar)',
                borderColor: isContextuallyActive ? 'var(--gb-gold-text-dark)' : 'rgba(255,255,255,.25)',
                boxShadow: isContextuallyActive ? '0 0 0 2px var(--gb-gold), 0 0 8px 2px rgba(242,177,52,.55)' : 'none',
              }}
            >
              {opponent.name.charAt(0).toUpperCase()}
            </button>
          );
        })}
      </div>

      {selectedIndex !== null && opponents[selectedIndex] && (
        <OpponentDetailModal
          opponents={opponents}
          selectedIndex={selectedIndex}
          onSelectIndex={setSelectedIndex}
          onClose={() => setSelectedIndex(null)}
        />
      )}
    </>
  );
}

export default OpponentIndexTabs;
