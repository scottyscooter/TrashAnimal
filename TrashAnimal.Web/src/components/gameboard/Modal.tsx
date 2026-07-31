import type { MouseEvent, ReactNode } from 'react';

interface ModalProps {
  children: ReactNode;
  onClose: () => void;
  labelledBy?: string;
  wide?: boolean;
  /** Overrides the wide/default width classes entirely (a Tailwind max-w-* class), for callers
   * that need to match a specific on-screen element's width rather than one of the two presets. */
  maxWidthClassName?: string;
  /** Shrinks the dialog to its content's width (still capped by the wide/maxWidthClassName ceiling)
   * instead of always stretching to fill it. Off by default so existing callers whose content relies
   * on the full width (stacked full-width buttons, side-by-side stat boxes) are unaffected — opt in
   * for content like a small card grid where a low item count would otherwise leave a lot of dead
   * space inside an unnecessarily wide dialog. */
  fitContent?: boolean;
}

/** Shared scrim + stopPropagation + glass-card modal shell, matching the design's modal spec:
 * full-screen scrim, click-scrim-to-close, click-inside-does-not-close. */
function Modal({ children, onClose, labelledBy, wide = false, maxWidthClassName, fitContent = false }: ModalProps) {
  function handleContentClick(event: MouseEvent) {
    event.stopPropagation();
  }

  const widthClassName = maxWidthClassName ?? (wide ? 'max-w-3xl' : 'max-w-md');

  return (
    <div
      role="presentation"
      onClick={onClose}
      className="fixed inset-0 z-40 flex items-center justify-center p-6"
      style={{ background: 'rgba(5,10,20,.68)', backdropFilter: 'blur(3px)' }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={labelledBy}
        onClick={handleContentClick}
        className={`relative rounded-[20px] border p-6 ${fitContent ? 'w-fit' : 'w-full'} ${widthClassName}`}
        style={{
          background: 'rgba(18,26,46,.95)',
          borderColor: 'rgba(255,255,255,.18)',
          boxShadow: 'var(--gb-modal-shadow)',
        }}
      >
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          className="absolute right-4 top-4 text-lg"
          style={{ color: 'var(--gb-text-label)' }}
        >
          ✕
        </button>
        {children}
      </div>
    </div>
  );
}

export default Modal;
