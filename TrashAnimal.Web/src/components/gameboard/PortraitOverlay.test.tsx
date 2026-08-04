import { describe, it, expect, vi } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen } from '../../test/test-utils';
import PortraitOverlay from './PortraitOverlay';

describe('PortraitOverlay', () => {
  it('shows a rotate message and a button to enter fullscreen landscape', () => {
    render(<PortraitOverlay />);

    expect(screen.getByRole('heading', { name: /rotate your device/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /enter fullscreen landscape/i })).toBeInTheDocument();
  });

  it('requests fullscreen and landscape orientation lock when the button is tapped', async () => {
    const user = userEvent.setup();
    const requestFullscreen = vi.fn().mockResolvedValue(undefined);
    const lock = vi.fn().mockResolvedValue(undefined);
    document.documentElement.requestFullscreen = requestFullscreen;
    Object.defineProperty(window, 'screen', {
      value: { ...window.screen, orientation: { lock } },
      writable: true,
      configurable: true,
    });

    render(<PortraitOverlay />);
    await user.click(screen.getByRole('button', { name: /enter fullscreen landscape/i }));

    expect(requestFullscreen).toHaveBeenCalledOnce();
    expect(lock).toHaveBeenCalledWith('landscape');
  });
});
