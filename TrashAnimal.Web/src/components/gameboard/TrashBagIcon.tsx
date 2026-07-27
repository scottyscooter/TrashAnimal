/**
 * The shared "your turn" animated icon (stink wisps + trash bag + glow ring + two flies), reused
 * identically by TurnIndicator and OpponentTile per the design handoff's "recreate as one shared
 * component" note.
 */
function TrashBagIcon() {
  return (
    <span
      aria-hidden="true"
      className="relative inline-block h-[38px] w-[34px] shrink-0"
      style={{
        boxShadow:
          '0 0 0 2px rgba(242,177,52,.65), 0 0 14px 4px rgba(242,177,52,.55)',
        borderRadius: '50%',
        background:
          'radial-gradient(circle, rgba(20,10,0,.55), rgba(20,10,0,.35), transparent)',
      }}
    >
      {[0, 0.4, 0.8].map((delay) => (
        <span
          key={delay}
          className="absolute left-1/2 top-1 -translate-x-1/2 text-[9px] font-bold"
          style={{
            color: 'var(--gb-stink-green)',
            animation: 'gb-stink-rise 1.6s ease-in-out infinite',
            animationDelay: `${delay}s`,
          }}
        >
          ~
        </span>
      ))}

      <span
        className="absolute bottom-[4px] left-1/2 h-[28px] w-[26px] -translate-x-1/2"
        style={{
          background: 'var(--gb-bag-dark)',
          borderRadius: '50% 50% 42% 42% / 60% 60% 40% 40%',
          boxShadow: 'inset -3px -3px 5px rgba(0,0,0,.4)',
        }}
      >
        <span
          className="absolute -top-[4px] left-1/2 h-[6px] w-[8px] -translate-x-1/2"
          style={{ background: 'var(--gb-bag-dark)', borderRadius: '2px' }}
        />
      </span>

      <span
        className="absolute h-[5px] w-[5px] rounded-full bg-black"
        style={{
          top: '14px',
          left: '17px',
          animation: 'gb-fly-orbit 2.2s linear infinite',
        }}
      />
      <span
        className="absolute h-[4px] w-[4px] rounded-full bg-black"
        style={{
          top: '10px',
          left: '10px',
          animation: 'gb-fly-erratic 2.9s ease-in-out infinite',
        }}
      />
    </span>
  );
}

export default TrashBagIcon;
