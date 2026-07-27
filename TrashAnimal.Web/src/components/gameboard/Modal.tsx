import type { MouseEvent, ReactNode } from 'react';

interface ModalProps {
  children: ReactNode;
  onClose: () => void;
  labelledBy?: string;
  wide?: boolean;
  /** Overrides the wide/default width classes entirely (a Tailwind max-w-* class), for callers
   * that need to match a specific on-screen element's width rather than one of the two presets. */
  maxWidthClassName?: string;
}

/** Shared scrim + stopPropagation + glass-card modal shell, matching the design's modal spec:
 * full-screen scrim, click-scrim-to-close, click-inside-does-not-close. */
function Modal({ children, onClose, labelledBy, wide = false, maxWidthClassName }: ModalProps) {
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
        className={`relative w-full rounded-[20px] border p-6 ${widthClassName}`}
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
