import type { TokenAction, TokenPhaseView } from '../../api/types';
import { TOKEN_ACTION_VALUES } from '../../api/types';
import { BUSTED_STAMP_IMAGE } from '../../pages/GameBoard/assetMaps';
import TokenSlot, { type TokenSlotState } from './TokenSlot';

interface TokenTrayProps {
  phaseOneTokens: TokenAction[];
  tokenPhase: TokenPhaseView | null;
  isBusted: boolean;
  size?: number;
  showBustedStamp?: boolean;
}

const TRAY_SLOT_COUNT = TOKEN_ACTION_VALUES.length;

/**
 * RollPhase and TokenPhase are two separate engine states, not one continuously-dimming tray, so
 * slot state is computed differently depending on which is active:
 * - RollPhase (tokenPhase === null): every rolled token is "active" (or "used"/dimmed if busted).
 * - TokenPhase: tokens still in remainingTokens or the current activeToken stay "active"; tokens
 *   no longer in either are already resolved and show as "used".
 */
function slotStateFor(
  token: TokenAction,
  tokenPhase: TokenPhaseView | null,
  isBusted: boolean,
): TokenSlotState {
  if (tokenPhase) {
    const stillPending = tokenPhase.remainingTokens.includes(token) || tokenPhase.activeToken === token;
    return stillPending ? 'active' : 'used';
  }
  return isBusted ? 'used' : 'active';
}

function TokenTray({ phaseOneTokens, tokenPhase, isBusted, size = 64, showBustedStamp = true }: TokenTrayProps) {
  const slots = Array.from({ length: TRAY_SLOT_COUNT }, (_, index) => phaseOneTokens[index] ?? null);

  return (
    <div className="relative flex" style={{ gap: size >= 64 ? 14 : 6 }}>
      {slots.map((token, index) => (
        <TokenSlot
          key={index}
          token={token}
          state={token ? slotStateFor(token, tokenPhase, isBusted) : 'empty'}
          size={size}
        />
      ))}
      {isBusted && showBustedStamp && (
        <img
          src={BUSTED_STAMP_IMAGE}
          alt="Busted"
          className="pointer-events-none absolute inset-0 m-auto h-auto max-h-[80%] w-auto max-w-[90%] object-contain"
        />
      )}
    </div>
  );
}

export default TokenTray;
