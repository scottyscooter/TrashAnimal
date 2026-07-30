import { describe, it, expect, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { ToastContext } from '../components/Toast/ToastContext';
import { useGameLogAnnouncements } from './useGameLogAnnouncements';
import type { GameLogEntryView } from '../api/types';

function entry(overrides: Partial<GameLogEntryView>): GameLogEntryView {
  return {
    sequenceNumber: 0,
    turnNumber: 1,
    actingPlayerSeat: 0,
    message: 'Something happened.',
    affectedPlayerSeat: null,
    ...overrides,
  };
}

// Renders the hook against a ToastContext value whose showToast IS the spy, so assertions can be
// made directly against calls the hook makes — no indirection through the real ToastProvider.
function renderWithToastSpy(initialEntries: GameLogEntryView[], localSeatIndex: number) {
  const showToastSpy = vi.fn();

  function wrapper({ children }: { children: ReactNode }) {
    return <ToastContext.Provider value={{ showToast: showToastSpy }}>{children}</ToastContext.Provider>;
  }

  const view = renderHook(({ entries }) => useGameLogAnnouncements(entries, localSeatIndex), {
    wrapper,
    initialProps: { entries: initialEntries },
  });

  return { showToastSpy, rerender: (entries: GameLogEntryView[]) => view.rerender({ entries }) };
}

describe('useGameLogAnnouncements', () => {
  it('seeds the high-water mark on mount without toasting, even with a non-empty backlog', () => {
    const backlog = [
      entry({ sequenceNumber: 1, actingPlayerSeat: 1, affectedPlayerSeat: 0, message: 'Bob did something to you.' }),
      entry({ sequenceNumber: 2, actingPlayerSeat: 1, affectedPlayerSeat: 0, message: 'Bob did something else to you.' }),
      entry({ sequenceNumber: 3, actingPlayerSeat: 1, affectedPlayerSeat: 0, message: 'the game has ended! You win!' }),
    ];

    const { showToastSpy } = renderWithToastSpy(backlog, 0);

    expect(showToastSpy).not.toHaveBeenCalled();
  });

  it('toasts a genuinely new entry that targets the local player and was not acted by them', () => {
    const initial = [entry({ sequenceNumber: 1, actingPlayerSeat: 1, affectedPlayerSeat: null, message: 'Bob rolled.' })];
    const { showToastSpy, rerender } = renderWithToastSpy(initial, 0);

    const nextEntry = entry({
      sequenceNumber: 2,
      actingPlayerSeat: 1,
      affectedPlayerSeat: 0,
      message: 'Bob played Doggo to block your steal.',
    });
    rerender([...initial, nextEntry]);

    expect(showToastSpy).toHaveBeenCalledTimes(1);
    expect(showToastSpy).toHaveBeenCalledWith(nextEntry.message, 'info');
  });

  it('does not toast an entry acted BY the local seat, even if affectedPlayerSeat equals their own seat', () => {
    const initial = [entry({ sequenceNumber: 1, actingPlayerSeat: 1, affectedPlayerSeat: null })];
    const { showToastSpy, rerender } = renderWithToastSpy(initial, 0);

    const nextEntry = entry({ sequenceNumber: 2, actingPlayerSeat: 0, affectedPlayerSeat: 0, message: 'You did something.' });
    rerender([...initial, nextEntry]);

    expect(showToastSpy).not.toHaveBeenCalled();
  });

  it('does not toast an entry whose affectedPlayerSeat targets a different seat', () => {
    const initial = [entry({ sequenceNumber: 1, actingPlayerSeat: 1, affectedPlayerSeat: null })];
    const { showToastSpy, rerender } = renderWithToastSpy(initial, 0);

    const nextEntry = entry({
      sequenceNumber: 2,
      actingPlayerSeat: 1,
      affectedPlayerSeat: 2,
      message: 'Bob did something to Carol.',
    });
    rerender([...initial, nextEntry]);

    expect(showToastSpy).not.toHaveBeenCalled();
  });

  it('toasts a game-end entry to every viewer regardless of a null affectedPlayerSeat, including the actor', () => {
    const initial = [entry({ sequenceNumber: 1, actingPlayerSeat: 1, affectedPlayerSeat: null })];
    const { showToastSpy, rerender } = renderWithToastSpy(initial, 0);

    const gameEndedEntry = entry({
      sequenceNumber: 2,
      actingPlayerSeat: 0, // local seat is the actor who emptied the deck
      affectedPlayerSeat: null,
      message: 'Your turn emptied the deck — the game has ended! You win!',
    });
    rerender([...initial, gameEndedEntry]);

    expect(showToastSpy).toHaveBeenCalledTimes(1);
    expect(showToastSpy).toHaveBeenCalledWith(gameEndedEntry.message, 'info');
  });

  it('respects the batch cap when many new entries arrive at once', () => {
    const initial = [entry({ sequenceNumber: 1, actingPlayerSeat: 1, affectedPlayerSeat: null })];
    const { showToastSpy, rerender } = renderWithToastSpy(initial, 0);

    const newEntries = [2, 3, 4, 5, 6].map((sequenceNumber) =>
      entry({ sequenceNumber, actingPlayerSeat: 1, affectedPlayerSeat: 0, message: `Event ${sequenceNumber}` }),
    );
    rerender([...initial, ...newEntries]);

    expect(showToastSpy.mock.calls.length).toBeGreaterThanOrEqual(2);
    expect(showToastSpy.mock.calls.length).toBeLessThanOrEqual(3);
    // The most recent entries win, not the oldest.
    const toastedMessages = showToastSpy.mock.calls.map(([message]) => message);
    expect(toastedMessages).toContain('Event 6');
    expect(toastedMessages).not.toContain('Event 2');
  });
});
