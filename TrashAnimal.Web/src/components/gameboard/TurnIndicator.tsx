import GlassPanel from './GlassPanel';
import TrashBagIcon from './TrashBagIcon';

interface TurnIndicatorProps {
  currentPlayerName: string;
  isLocalPlayerTurn: boolean;
}

function TurnIndicator({ currentPlayerName, isLocalPlayerTurn }: TurnIndicatorProps) {
  return (
    <GlassPanel className="fixed left-1/2 top-6 z-20 flex -translate-x-1/2 items-center gap-2 rounded-full px-[26px] py-3">
      <span
        className="gb-turn-dot h-[10px] w-[10px] shrink-0 rounded-full"
        style={{ background: 'var(--gb-green)' }}
        aria-hidden="true"
      />
      <span
        className="text-[20px] font-semibold tracking-[0.06em]"
        style={{ color: 'var(--gb-text-primary)' }}
      >
        {isLocalPlayerTurn ? 'YOUR TURN' : `${currentPlayerName.toUpperCase()}'S TURN`}
      </span>
      {isLocalPlayerTurn && <TrashBagIcon />}
    </GlassPanel>
  );
}

export default TurnIndicator;
