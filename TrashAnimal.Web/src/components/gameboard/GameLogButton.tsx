interface GameLogButtonProps {
  onClick: () => void;
}

/**
 * Phone-landscape-only icon trigger for the game log focus panel (`GameLogFocusPanel`). Dumb
 * trigger component — it owns no open/close state itself, matching the codebase's convention of
 * container components (here, `GameBoardPage`) owning state and leaf components taking callbacks.
 *
 * Positioned beside `GameBoardThemeToggle` (`fixed right-6 top-6`, 60px circle, z-20) without
 * overlapping it — this button sits to its immediate left at a smaller, phone-landscape-appropriate
 * size. Hidden outside phone landscape via CSS only (`hidden phone-landscape:flex`), same pattern
 * as `OpponentIndexTabs`, since nothing here needs JS-level orientation awareness.
 */
function GameLogButton({ onClick }: GameLogButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label="Open game log"
      className="gb-glass fixed right-[92px] top-[18px] z-20 hidden h-11 w-11 items-center justify-center rounded-full phone-landscape:flex"
    >
      <span className="flex flex-col gap-[3px]" aria-hidden="true">
        <span className="h-[2px] w-[18px] rounded-full" style={{ background: 'var(--gb-text-primary)' }} />
        <span className="h-[2px] w-[14px] rounded-full" style={{ background: 'var(--gb-text-primary)' }} />
        <span className="h-[2px] w-[18px] rounded-full" style={{ background: 'var(--gb-text-primary)' }} />
      </span>
    </button>
  );
}

export default GameLogButton;
