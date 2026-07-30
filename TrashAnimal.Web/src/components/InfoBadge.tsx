import { useEffect, useRef, useState, type ReactNode } from 'react';

interface InfoBadgeProps {
  info: string | null | undefined;
  children: ReactNode;
}

/** General-purpose "attach an on-demand info bubble to any element" primitive — has no idea what
 * the text it displays means. Renders children unwrapped with no extra markup when `info` is
 * falsy, so callers can pass a possibly-null value unconditionally without branching.
 *
 * Three ways to reveal the bubble: mouse hover and keyboard focus both reveal it transiently
 * (closes again on mouse-leave/blur); a click/tap instead pins it open regardless of hover/focus
 * state, so touch-only users (who can't hover) still have a reliable way to read it — dismissed by
 * clicking the badge again, clicking elsewhere, or Escape. Bubble always opens upward
 * (`bottom: 100%`), anchored top-right of the wrapped element — no collision detection. */
function InfoBadge({ info, children }: InfoBadgeProps) {
  const [isHovering, setIsHovering] = useState(false);
  const [isFocused, setIsFocused] = useState(false);
  const [isPinned, setIsPinned] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const isVisible = Boolean(info) && (isHovering || isFocused || isPinned);

  useEffect(() => {
    if (!isPinned) {
      return;
    }

    function handlePointerDown(event: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(event.target as Node)) {
        setIsPinned(false);
      }
    }

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        setIsPinned(false);
      }
    }

    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [isPinned]);

  if (!info) {
    return <>{children}</>;
  }

  return (
    <div ref={containerRef} className="relative" onMouseEnter={() => setIsHovering(true)} onMouseLeave={() => setIsHovering(false)}>
      {children}
      <button
        type="button"
        aria-label="More information"
        onClick={(event) => {
          event.stopPropagation();
          setIsPinned((current) => !current);
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
