import { useEffect, useState } from 'react';
import Modal from './Modal';

/** Cycles 1, 2, 3 dots on a fixed interval — a simple loading-style animated ellipsis. */
function useAnimatedEllipsis(): string {
  const [dotCount, setDotCount] = useState(1);

  useEffect(() => {
    const id = setInterval(() => setDotCount((count) => (count % 3) + 1), 400);
    return () => clearInterval(id);
  }, []);

  return '.'.repeat(dotCount);
}

/** Shown to the active player while a Bandit token they resolved is waiting on an opponent's
 * stash-or-pass response — the responder themselves sees BanditResponseModal instead. */
function BanditWaitingModal() {
  const dots = useAnimatedEllipsis();

  return (
    <Modal onClose={() => {}} labelledBy="bandit-waiting-heading">
      <h2 id="bandit-waiting-heading" className="text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
        Waiting for opponents to stash or pass
        <span className="inline-block w-6 text-left" aria-hidden="true">
          {dots}
        </span>
        <span className="sr-only">, please wait</span>
      </h2>
    </Modal>
  );
}

export default BanditWaitingModal;
