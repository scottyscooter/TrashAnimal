import type { GameAction } from '../../api/types';
import GlassPanel from './GlassPanel';

interface YumYumPromptProps {
  allowedActions: GameAction[];
  onAction: (action: GameAction) => void;
  isPending: boolean;
}

/** No design mock exists for this prompt — minimal inline panel styled to the shared palette. */
function YumYumPrompt({ allowedActions, onAction, isPending }: YumYumPromptProps) {
  return (
    <GlassPanel className="fixed left-1/2 top-[140px] z-30 flex -translate-x-1/2 flex-col items-center gap-3 rounded-2xl px-6 py-4">
      <p className="text-sm font-semibold tracking-[0.06em]" style={{ color: 'var(--gb-text-primary)' }}>
        The active player stopped rolling — play Yum Yum?
      </p>
      <div className="flex gap-3">
        {allowedActions.includes('YumYumPlay') && (
          <button
            type="button"
            disabled={isPending}
            onClick={() => onAction('YumYumPlay')}
            className="rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
            style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
          >
            Play Yum Yum
          </button>
        )}
        <button
          type="button"
          disabled={isPending}
          onClick={() => onAction('YumYumPass')}
          className="gb-glass rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
          style={{ color: 'var(--gb-text-primary)' }}
        >
          Pass
        </button>
      </div>
    </GlassPanel>
  );
}

export default YumYumPrompt;
