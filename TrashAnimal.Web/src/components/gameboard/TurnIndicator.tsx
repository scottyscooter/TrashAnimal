import type { GameState } from '../../api/types';
import { useIsPhoneLandscape } from '../../hooks/useLandscapeBreakpoint';
import GlassPanel from './GlassPanel';
import { RESOLVING_STATES } from './gamePhaseStatus';
import TrashBagIcon from './TrashBagIcon';

interface TurnIndicatorProps {
  currentPlayerName: string;
  isLocalPlayerTurn: boolean;
  state?: GameState;
}

function TurnIndicator({ currentPlayerName, isLocalPlayerTurn, state }: TurnIndicatorProps) {
  const isPhoneLandscape = useIsPhoneLandscape();
  const phaseName = state
    ? state
        .replace(/([A-Z])/g, ' $1')
        .trim()
        .split(' ')
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
        .join(' ')
    : '';

  // PhaseToggle's own Rolling/Resolving pill is hidden on phone landscape (no vertical room for a
  // second floating widget there) — fold the same distinction into this subtitle instead, only
  // when it's the local player's turn, matching PhaseToggle's own render condition in GameBoardPage.
  const rollingResolvingSuffix =
    isLocalPlayerTurn && state ? (RESOLVING_STATES.includes(state) ? ' · RESOLVING' : ' · ROLLING') : '';

  return (
    <div className="fixed left-1/2 top-6 z-20 flex -translate-x-1/2 flex-col items-center phone-landscape:top-[4%]">
      <GlassPanel className="flex items-center gap-2 rounded-full px-[26px] py-3 phone-landscape:px-4 phone-landscape:py-1.5">
        <span
          className="gb-turn-dot h-[10px] w-[10px] shrink-0 rounded-full"
          style={{ background: 'var(--gb-green)' }}
          aria-hidden="true"
        />
        <span
          className="text-[20px] font-semibold tracking-[0.06em] phone-landscape:text-[16px]"
          style={{ color: 'var(--gb-text-primary)' }}
        >
          {isLocalPlayerTurn ? 'YOUR TURN' : `${currentPlayerName.toUpperCase()}'S TURN`}
        </span>
        {/* Native 38×34: unscaled, that's taller than everything else in this pill (the 16px
            phone-landscape text, the tightened py-1.5) and was inflating the whole pill's height
            on phone landscape only for the local player's turn — see TrashBagIcon's `scale` prop. */}
        {isLocalPlayerTurn && <TrashBagIcon scale={isPhoneLandscape ? 0.5 : 1} />}
      </GlassPanel>
      {phaseName && (
        <span
          className="mt-1 text-xs font-semibold tracking-[0.08em] phone-landscape:block hidden"
          style={{ color: 'var(--gb-text-label)' }}
        >
          {phaseName}
          {rollingResolvingSuffix}
        </span>
      )}
    </div>
  );
}

export default TurnIndicator;
