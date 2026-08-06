import { describe, it, expect } from 'vitest';
import { fireEvent } from '@testing-library/react';
import { render, screen } from '../../test/test-utils';
import GameLogEntryList from './GameLogEntryList';
import type { GameLogEntryView } from '../../api/types';

function makeEntry(sequenceNumber: number, message: string): GameLogEntryView {
  return {
    sequenceNumber,
    turnNumber: 1,
    actingPlayerSeat: 0,
    message,
    affectedPlayerSeat: null,
  };
}

/** jsdom doesn't lay anything out, so scrollHeight/clientHeight stay 0 unless stubbed. */
function stubOverflow(list: HTMLElement, scrollHeight: number, clientHeight = 100) {
  Object.defineProperty(list, 'scrollHeight', { value: scrollHeight, configurable: true });
  Object.defineProperty(list, 'clientHeight', { value: clientHeight, configurable: true });
}

describe('GameLogEntryList', () => {
  it('renders the newest entry first', () => {
    const entries = [makeEntry(1, 'oldest'), makeEntry(2, 'middle'), makeEntry(3, 'newest')];
    render(<GameLogEntryList entries={entries} />);

    const items = screen.getAllByRole('listitem');
    expect(items[0]).toHaveTextContent('newest');
    expect(items[2]).toHaveTextContent('oldest');
  });

  it('stays pinned to the top when new entries arrive while already at the top', () => {
    const entries = [makeEntry(1, 'oldest'), makeEntry(2, 'newest')];
    const { rerender, container } = render(<GameLogEntryList entries={entries} />);
    const list = container.querySelector('ul') as HTMLUListElement;
    stubOverflow(list, 400);
    Object.defineProperty(list, 'scrollTop', { value: 0, writable: true, configurable: true });

    stubOverflow(list, 600);
    rerender(<GameLogEntryList entries={[...entries, makeEntry(3, 'newer still')]} />);

    expect(list.scrollTop).toBe(0);
  });

  it('holds the reading position steady when new entries arrive while scrolled away', () => {
    const entries = [makeEntry(1, 'oldest'), makeEntry(2, 'newest')];
    const { rerender, container } = render(<GameLogEntryList entries={entries} />);
    const list = container.querySelector('ul') as HTMLUListElement;
    stubOverflow(list, 400);
    rerender(<GameLogEntryList entries={[...entries]} />);
    Object.defineProperty(list, 'scrollTop', { value: 200, writable: true, configurable: true });
    fireEvent.scroll(list);

    stubOverflow(list, 600);
    rerender(<GameLogEntryList entries={[...entries, makeEntry(3, 'newer still')]} />);

    expect(list.scrollTop).toBe(400);
  });

  it('re-pins to the top after the user scrolls back near it', () => {
    const entries = [makeEntry(1, 'oldest'), makeEntry(2, 'newest')];
    const { rerender, container } = render(<GameLogEntryList entries={entries} />);
    const list = container.querySelector('ul') as HTMLUListElement;
    stubOverflow(list, 400);
    rerender(<GameLogEntryList entries={[...entries]} />);

    Object.defineProperty(list, 'scrollTop', { value: 200, writable: true, configurable: true });
    fireEvent.scroll(list);

    Object.defineProperty(list, 'scrollTop', { value: 2, writable: true, configurable: true });
    fireEvent.scroll(list);

    stubOverflow(list, 600);
    rerender(<GameLogEntryList entries={[...entries, makeEntry(3, 'newer still')]} />);

    expect(list.scrollTop).toBe(0);
  });

  it('renders as an accessible, live-updating list', () => {
    render(<GameLogEntryList entries={[makeEntry(1, 'a message')]} />);

    const list = screen.getByLabelText('Game log');
    expect(list).toHaveAttribute('aria-live', 'polite');
  });
});
