import type { GameState } from '../../api/types';
import { RESOLVING_STATES } from './gamePhaseStatus';
import GlassPanel from './GlassPanel';

interface PhaseToggleProps {
  state: GameState;
}

/** Rolling/Resolving segmented pill — active segment is derived from GameState (server-driven),
 * not from local tray fullness like the design mock reactively infers.
 *
 * Hidden on phone landscape (`phone-landscape:hidden`) — TurnIndicator's subtitle shows the same
 * information there instead, since this component's fixed `top-[112px]` collides with the hand. */
function PhaseToggle({ state }: PhaseToggleProps) {
  const isResolving = RESOLVING_STATES.includes(state);

  return (
    <GlassPanel className="fixed left-1/2 top-[112px] z-20 flex w-[220px] -translate-x-1/2 overflow-hidden rounded-full p-0 phone-landscape:hidden">
      {(['Rolling', 'Resolving'] as const).map((label, index) => {
        const active = index === 0 ? !isResolving : isResolving;
        return (
          <span
            key={label}
            className="flex-1 border-r border-[rgba(255,255,255,.25)] py-2 text-center text-[12px] font-bold tracking-[0.12em] transition-colors duration-[250ms] last:border-r-0"
            style={{
              background: active ? (index === 0 ? 'var(--gb-green)' : 'var(--gb-gold)') : 'transparent',
              color: active ? (index === 0 ? 'var(--gb-green-text)' : 'var(--gb-gold-text)') : '#cfd8e8',
            }}
          >
            {label.toUpperCase()}
          </span>
        );
      })}
    </GlassPanel>
  );
}

export default PhaseToggle;
