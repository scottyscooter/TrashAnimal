import type { GameAction } from '../../api/types';

interface RollStopControlsProps {
  allowedActions: GameAction[];
  onAction: (action: GameAction) => void;
  isPending: boolean;
}

/** The "primary" action button's label/action swaps by allowed-action priority (server-driven,
 * never inferred from local tray fullness): EndTurn > AdvanceToResolveTokens > RollDie. */
function primaryAction(allowedActions: GameAction[]): { label: string; action: GameAction } | null {
  if (allowedActions.includes('EndTurn')) return { label: 'NEW TURN', action: 'EndTurn' };
  if (allowedActions.includes('AdvanceToResolveTokens')) {
    return { label: 'ADVANCE', action: 'AdvanceToResolveTokens' };
  }
  if (allowedActions.includes('RollDie')) return { label: 'ROLL', action: 'RollDie' };
  return null;
}

function RollStopControls({ allowedActions, onAction, isPending }: RollStopControlsProps) {
  const primary = primaryAction(allowedActions);
  const canStop = allowedActions.includes('StopRolling');

  return (
    <div className="fixed bottom-[60px] right-[80px] z-20 flex flex-col items-center gap-5 phone-landscape:bottom-[6%] phone-landscape:right-[2%] phone-landscape:flex-row phone-landscape:gap-2">
      {allowedActions.includes('AbandonBust') && (
        <div className="flex gap-2 phone-landscape:gap-1">
          <button
            type="button"
            disabled={isPending}
            onClick={() => onAction('AbandonBust')}
            className="rounded-lg px-3 py-2 text-xs font-bold tracking-[0.06em] text-white disabled:opacity-50 phone-landscape:px-2 phone-landscape:py-1 phone-landscape:text-[10px]"
            style={{ background: 'var(--gb-red)' }}
          >
            DRAW 1 & END TURN
          </button>
        </div>
      )}

      <div className="flex items-end gap-5">
        <button
          type="button"
          disabled={!canStop || isPending}
          onClick={() => onAction('StopRolling')}
          aria-label="Stop rolling"
          className="flex h-[104px] w-[104px] items-center justify-center text-sm font-bold tracking-[0.06em] text-white transition-opacity phone-landscape:h-[60px] phone-landscape:w-[60px] phone-landscape:text-xs"
          style={{
            clipPath: 'polygon(30% 0,70% 0,100% 30%,100% 70%,70% 100%,30% 100%,0 70%,0 30%)',
            background: 'var(--gb-red)',
            opacity: canStop ? 1 : 0.5,
            cursor: canStop ? 'pointer' : 'default',
          }}
        >
          STOP
        </button>

        <button
          type="button"
          disabled={!primary || isPending}
          onClick={() => primary && onAction(primary.action)}
          className="flex h-[88px] w-[120px] flex-col items-center justify-center gap-1 rounded-xl border-[3px] text-sm font-bold tracking-[0.06em] disabled:opacity-50 phone-landscape:h-[60px] phone-landscape:w-[70px] phone-landscape:border-[2px] phone-landscape:text-xs phone-landscape:gap-0"
          style={{
            background: 'linear-gradient(160deg,#ffd873,#f2b134)',
            borderColor: '#a86e12',
            color: 'var(--gb-gold-text-dark)',
            boxShadow: '0 8px 0 #a86e12, 0 10px 14px rgba(0,0,0,.35)',
          }}
        >
          <span
            className="grid h-[18px] w-[18px] grid-cols-3 grid-rows-3 gap-[2px] phone-landscape:h-[12px] phone-landscape:w-[12px] phone-landscape:gap-[1px]"
            aria-hidden="true"
          >
            {[1, 1, 1, 0, 1, 0, 1, 1, 1].map((filled, index) => (
              <span
                key={index}
                className="rounded-full"
                style={{ background: filled ? 'var(--gb-gold-text-dark)' : 'transparent' }}
              />
            ))}
          </span>
          {primary?.label ?? 'ROLL'}
        </button>
      </div>
    </div>
  );
}

export default RollStopControls;
