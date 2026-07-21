import { describe, it, expect } from 'vitest';
import { render, screen } from '../test/test-utils';
import PlayerList from './PlayerList';

describe('PlayerList', () => {
  const seats = [
    { seatIndex: 0, nickname: 'Alice' },
    { seatIndex: 1, nickname: 'Bob' },
  ];

  it('renders every seated player', () => {
    render(<PlayerList seats={seats} mySeatIndex={null} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('labels seat 0 as Host', () => {
    render(<PlayerList seats={seats} mySeatIndex={null} />);
    expect(screen.getByText('Host')).toBeInTheDocument();
  });

  it('labels the current player as You', () => {
    render(<PlayerList seats={seats} mySeatIndex={1} />);
    expect(screen.getByText('You')).toBeInTheDocument();
  });
});
