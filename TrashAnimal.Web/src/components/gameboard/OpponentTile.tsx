import type { GameView, OpponentSummaryView } from '../../api/types';
import { opponentColorForSeat } from '../../pages/GameBoard/assetMaps';
import GlassPanel from './GlassPanel';
import TokenTray from './TokenTray';
import TrashBagIcon from './TrashBagIcon';

interface OpponentTileProps {
  opponent: OpponentSummaryView;
  gameView: GameView;
  onClick: () => void;
}

function OpponentTile({ opponent, gameView, onClick }: OpponentTileProps) {
  const isCurrentTurn = gameView.currentPlayerIndex === opponent.seatIndex;
  const stashTotal = opponent.stashFaceDownCount + opponent.stashFaceUpCards.length;

  return (
    <GlassPanel hoverable onClick={onClick} className="flex flex-col gap-2 rounded-2xl p-3.5">
      <div className="flex items-center gap-2">
        <span
          className="flex h-[38px] w-[38px] shrink-0 items-center justify-center rounded-full text-sm font-bold"
          style={{ background: opponentColorForSeat(opponent.seatIndex), color: 'var(--gb-text-on-avatar)' }}
        >
          {opponent.name.charAt(0).toUpperCase()}
        </span>
        <span className="truncate text-[16px] font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
          {opponent.name}
        </span>
        {isCurrentTurn && <TrashBagIcon />}
      </div>

      <div className="flex gap-2">
        <span
          className="rounded-full border px-2.5 py-0.5 text-[11px] font-bold"
          style={{ background: 'rgba(255,255,255,.1)', borderColor: 'rgba(255,255,255,.18)', color: 'var(--gb-text-primary)' }}
        >
          HAND {opponent.handCount}
        </span>
        <span
          className="rounded-full border px-2.5 py-0.5 text-[11px] font-bold"
          style={{ background: 'rgba(255,255,255,.1)', borderColor: 'rgba(255,255,255,.18)', color: 'var(--gb-text-primary)' }}
        >
          STASH {stashTotal}
        </span>
      </div>

      {/* Always rendered (6 slots) per the design — empty for whichever opponents aren't
          currently rolling, filled for whoever is, matching the design's Row 3 spec. */}
      <TokenTray
        phaseOneTokens={isCurrentTurn ? gameView.phaseOneTokens : []}
        tokenPhase={isCurrentTurn ? gameView.tokenPhase : null}
        isBusted={isCurrentTurn && gameView.isBusted}
        size={26}
        showBustedStamp={false}
      />
    </GlassPanel>
  );
}

export default OpponentTile;
