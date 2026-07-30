import { useEffect, useRef, useState, type ReactNode } from 'react';

interface InfoBadgeProps {
  info: string | null | undefined;
  children: ReactNode;
}

type PinState = 'unset' | 'pinned' | 'dismissed';

/** General-purpose "attach an on-demand info bubble to any element" primitive — has no idea what
 * the text it displays means. Renders children unwrapped with no extra markup when `info` is
 * falsy, so callers can pass a possibly-null value unconditionally without branching.
 *
 * Three ways to reveal the bubble: mouse hover and keyboard focus both reveal it transiently
 * (closes again on mouse-leave/blur, which also clears any explicit pin/dismiss so a later hover
 * behaves normally); a click/tap instead pins it open regardless of hover/focus state, so
 * touch-only users (who can't hover) still have a reliable way to read it. Clicking again while
 * visible dismisses it outright — beating hover, so it stays hidden while the pointer remains over
 * the wrapped element — and clicking elsewhere or pressing Escape both reset to the default
 * (hover/focus-driven) state rather than dismissing permanently. Bubble always opens upward
 * (`bottom: 100%`), anchored top-right of the wrapped element — no collision detection. */
function InfoBadge({ info, children }: InfoBadgeProps) {
  const [isHovering, setIsHovering] = useState(false);
  const [isFocused, setIsFocused] = useState(false);
  const [pinState, setPinState] = useState<PinState>('unset');
  const containerRef = useRef<HTMLDivElement>(null);

  const isVisible = Boolean(info) && pinState !== 'dismissed' && (isHovering || isFocused || pinState === 'pinned');

  useEffect(() => {
    if (pinState === 'unset') {
      return;
    }

    function handlePointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setPinState('unset');
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setPinState('unset');
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [pinState]);

  if (!info) {
    return <>{children}</>;
  }

  return (
    <div
      ref={containerRef}
      className="relative"
      onMouseEnter={() => setIsHovering(true)}
      onMouseLeave={() => {
        setIsHovering(false);
        // Only a 'dismissed' pin exists to suppress the bubble while the pointer stays over the
        // card — once the pointer leaves, that purpose is served, so clear it for the next hover.
        // A 'pinned' bubble was explicitly opened by a click and should survive mouse-leave.
        setPinState((current) => (current === 'dismissed' ? 'unset' : current));
      }}
    >
      {children}
      <button
        type="button"
        aria-label="More information"
        onClick={(event) => {
          event.stopPropagation();
          setPinState(isVisible ? 'dismissed' : 'pinned');
        }}
        onFocus={() => setIsFocused(true)}
        onBlur={() => setIsFocused(false)}
        className="gb-glass gb-glass-hover absolute right-1 top-1 z-10 flex h-5 w-5 items-center justify-center rounded-full text-[11px] font-bold"
        style={{ color: 'var(--gb-text-primary)' }}
      >
        i
      </button>
      {isVisible && (
        <div
          role="tooltip"
          className="gb-glass absolute right-0 z-20 w-max max-w-[220px] rounded-lg px-3 py-2 text-xs"
          style={{ bottom: '100%', color: 'var(--gb-text-label)' }}
        >
          {info}
        </div>
      )}
    </div>
  );
}

export default InfoBadge;
