import type { LobbySeatView } from '../api/types';

interface PlayerListProps {
  seats: LobbySeatView[];
  mySeatIndex: number | null;
}

function PlayerList({ seats, mySeatIndex }: PlayerListProps) {
  return (
    <ul className="flex flex-col gap-2">
      {seats.map((seat) => (
        <li
          key={seat.seatIndex}
          className="flex items-center justify-between rounded-md border border-[var(--border)] px-3 py-2"
        >
          <span>
            {seat.nickname}
            {seat.seatIndex === 0 && <span className="ml-2 text-xs text-[var(--accent)]">Host</span>}
          </span>
          {seat.seatIndex === mySeatIndex && <span className="text-xs opacity-70">You</span>}
        </li>
      ))}
    </ul>
  );
}

export default PlayerList;
