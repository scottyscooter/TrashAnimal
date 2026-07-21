import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '../test/test-utils';
import ShareLink from './ShareLink';

describe('ShareLink', () => {
  beforeEach(() => {
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) } });
  });

  it('shows the full share URL for the lobby', () => {
    render(<ShareLink lobbyId="abc-123" />);
    const input = screen.getByLabelText(/lobby share link/i) as HTMLInputElement;
    expect(input.value).toContain('/games/abc-123/lobby');
  });

  it('copies the share URL to the clipboard and shows confirmation', async () => {
    render(<ShareLink lobbyId="abc-123" />);

    screen.getByRole('button', { name: /copy/i }).click();

    expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      expect.stringContaining('/games/abc-123/lobby'),
    );
    expect(await screen.findByRole('button', { name: /copied/i })).toBeInTheDocument();
  });
});
