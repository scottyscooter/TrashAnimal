import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '../../test/test-utils';
import { useToast } from './useToast';

function ToastTrigger({ text }: { text: string }) {
  const { showToast } = useToast();
  return (
    <button type="button" onClick={() => showToast(text)}>
      Trigger
    </button>
  );
}

describe('ToastProvider', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders a toast message when showToast is called', async () => {
    render(<ToastTrigger text="Something went wrong" />);

    fireEvent.click(screen.getByRole('button', { name: /trigger/i }));

    expect(await screen.findByText('Something went wrong')).toBeInTheDocument();
  });

  it('auto-dismisses the toast after a delay', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    render(<ToastTrigger text="Disappearing message" />);

    fireEvent.click(screen.getByRole('button', { name: /trigger/i }));
    expect(await screen.findByText('Disappearing message')).toBeInTheDocument();

    vi.advanceTimersByTime(5000);

    await waitFor(() => {
      expect(screen.queryByText('Disappearing message')).not.toBeInTheDocument();
    });
  });
});
