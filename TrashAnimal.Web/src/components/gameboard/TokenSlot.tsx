import type { CSSProperties } from 'react';
import type { TokenAction } from '../../api/types';
import { TOKEN_IMAGE_BY_ACTION } from '../../pages/GameBoard/assetMaps';

export type TokenSlotState = 'empty' | 'active' | 'used';

interface TokenSlotProps {
  token: TokenAction | null;
  state: TokenSlotState;
  size?: number;
}

/** One circular token slot — empty / filled-active / used-dimmed, per the design's Token Slot
 * States spec. Reusable at any size (64px player tray, 26px opponent tiles). */
function TokenSlot({ token, state, size = 64 }: TokenSlotProps) {
  const style: CSSProperties = { width: size, height: size };

  if (state === 'empty' || !token) {
    return (
      <span
        className="inline-block shrink-0 rounded-full border-2 border-dashed"
        style={{ ...style, borderColor: 'rgba(255,255,255,.4)' }}
      />
    );
  }

  const isActive = state === 'active';

  return (
    <span
      className={`inline-block shrink-0 overflow-hidden rounded-full border-2 ${isActive ? 'gb-token-pop' : ''}`}
      style={{
        ...style,
        background: isActive ? '#1e2536' : 'var(--gb-slot-used-bg)',
        borderColor: isActive ? 'var(--gb-gold)' : 'rgba(255,255,255,.15)',
        opacity: isActive ? 1 : 0.4,
        boxShadow: isActive ? '0 4px 8px rgba(0,0,0,.35)' : 'none',
      }}
      title={token}
    >
      <img
        src={TOKEN_IMAGE_BY_ACTION[token]}
        alt={token}
        className="h-full w-full object-cover"
      />
    </span>
  );
}

export default TokenSlot;
