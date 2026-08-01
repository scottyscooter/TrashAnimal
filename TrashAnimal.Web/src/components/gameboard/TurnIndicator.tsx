import type { GameState } from '../../api/types';
import GlassPanel from './GlassPanel';
import TrashBagIcon from './TrashBagIcon';

interface TurnIndicatorProps {
  currentPlayerName: string;
  isLocalPlayerTurn: boolean;
  state?: GameState;
}

function TurnIndicator({ currentPlayerName, isLocalPlayerTurn, state }: TurnIndicatorProps) {
  const phaseName = state
    ? state
        .replace(/([A-Z])/g, ' $1')
        .trim()
        .split(' ')
        .map((word) => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
        .join(' ')
    : '';

  return (
    <div className="fixed left-1/2 top-6 z-20 flex -translate-x-1/2 flex-col items-center phone-landscape:top-[4%]">
      <GlassPanel className="flex items-center gap-2 rounded-full px-[26px] py-3">
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
        {isLocalPlayerTurn && <TrashBagIcon />}
      </GlassPanel>
      {phaseName && (
        <span
          className="mt-1 text-xs font-semibold tracking-[0.08em] phone-landscape:block hidden"
          style={{ color: 'var(--gb-text-label)' }}
        >
          {phaseName}
        </span>
      )}
    </div>
  );
}

export default TurnIndicator;
