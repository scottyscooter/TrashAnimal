import { useState } from 'react';
import type { GameView } from '../../api/types';
import OpponentDetailModal from './OpponentDetailModal';
import OpponentTile from './OpponentTile';

interface OpponentRailProps {
  gameView: GameView;
}

function OpponentRail({ gameView }: OpponentRailProps) {
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);

  return (
    <>
      <div className="fixed left-7 top-[110px] z-10 flex w-[236px] flex-col gap-4 phone-landscape:hidden">
        {gameView.opponents.map((opponent, index) => (
          <OpponentTile
            key={opponent.seatIndex}
            opponent={opponent}
            gameView={gameView}
            onClick={() => setSelectedIndex(index)}
          />
        ))}
      </div>

      {selectedIndex !== null && gameView.opponents[selectedIndex] && (
        <OpponentDetailModal
          opponents={gameView.opponents}
          selectedIndex={selectedIndex}
          onSelectIndex={setSelectedIndex}
          onClose={() => setSelectedIndex(null)}
        />
      )}
    </>
  );
}

export default OpponentRail;
