import type { ReactNode } from 'react';

interface GlassPanelProps {
  children: ReactNode;
  className?: string;
  hoverable?: boolean;
  onClick?: () => void;
}

function GlassPanel({ children, className = '', hoverable = false, onClick }: GlassPanelProps) {
  const classes = ['gb-glass', hoverable && 'gb-glass-hover', className].filter(Boolean).join(' ');

  if (onClick) {
    return (
      <button type="button" onClick={onClick} className={`${classes} cursor-pointer text-left`}>
        {children}
      </button>
    );
  }

  return <div className={classes}>{children}</div>;
}

export default GlassPanel;
