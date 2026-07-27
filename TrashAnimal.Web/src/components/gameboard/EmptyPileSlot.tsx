interface EmptyPileSlotProps {
  className?: string;
}

/** Dashed-border empty-pile placeholder, matching the design's Token Slot "empty" treatment —
 * shared by the face-up stash, face-down stash, and discard piles for when they hold no cards. */
function EmptyPileSlot({ className = '' }: EmptyPileSlotProps) {
  return (
    <div
      className={`flex items-center justify-center border-2 border-dashed ${className}`}
      style={{ borderColor: 'rgba(255,255,255,.3)' }}
    />
  );
}

export default EmptyPileSlot;
