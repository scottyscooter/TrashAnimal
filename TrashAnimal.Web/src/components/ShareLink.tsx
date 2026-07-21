import { useState } from 'react';

interface ShareLinkProps {
  lobbyId: string;
}

function ShareLink({ lobbyId }: ShareLinkProps) {
  const [copied, setCopied] = useState(false);
  const shareUrl = `${window.location.origin}/games/${lobbyId}/lobby`;

  async function handleCopy() {
    await navigator.clipboard.writeText(shareUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="flex items-center gap-2 rounded-md border border-[var(--border)] px-3 py-2">
      <input
        type="text"
        readOnly
        value={shareUrl}
        aria-label="Lobby share link"
        className="flex-1 truncate bg-transparent text-sm outline-none"
      />
      <button
        type="button"
        onClick={handleCopy}
        className="shrink-0 rounded-md bg-[var(--accent)] px-3 py-1 text-sm font-medium text-white"
      >
        {copied ? 'Copied!' : 'Copy'}
      </button>
    </div>
  );
}

export default ShareLink;
