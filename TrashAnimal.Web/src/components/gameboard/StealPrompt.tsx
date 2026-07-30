import type { GameAction, GameState, StealPhaseView } from '../../api/types';
import Modal from './Modal';
import StealPickFan from './StealPickFan';

interface StealPromptProps {
  state: GameState;
  stealPhase: StealPhaseView;
  localSeatIndex: number;
  allowedActions: GameAction[];
  onAction: (action: GameAction) => void;
  onCardPick: (cardId: string) => void;
  isPending: boolean;
}

/** Renders whichever side of the steal interrupt applies to the local player — victim response,
 * thief card pick, or a waiting state for everyone else. */
function StealPrompt({
  state,
  stealPhase,
  localSeatIndex,
  allowedActions,
  onAction,
  onCardPick,
  isPending,
}: StealPromptProps) {
  const isVictim = stealPhase.victimIndex === localSeatIndex;
  const isThief = stealPhase.stealingPlayerIndex === localSeatIndex;

  if (state === 'AwaitingStealResponse' && isVictim) {
    return (
      <Modal onClose={() => {}} labelledBy="steal-response-heading">
        <h2 id="steal-response-heading" className="mb-3 text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
          {stealPhase.stealingPlayerName} wants to steal from your {stealPhase.initialStealTargetZone.toLowerCase()}
        </h2>
        <div className="flex flex-wrap gap-2">
          {allowedActions.includes('StealPlayDoggo') && (
            <button
              type="button"
              disabled={isPending}
              onClick={() => onAction('StealPlayDoggo')}
              className="rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
              style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
            >
              Play Doggo (block)
            </button>
          )}
          {allowedActions.includes('StealPlayKitteh') && (
            <button
              type="button"
              disabled={isPending}
              onClick={() => onAction('StealPlayKitteh')}
              className="rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
              style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
            >
              Play Kitteh (swap)
            </button>
          )}
          <button
            type="button"
            disabled={isPending}
            onClick={() => onAction('StealPass')}
            className="gb-glass rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
            style={{ color: 'var(--gb-text-primary)' }}
          >
            Let it happen
          </button>
        </div>
      </Modal>
    );
  }

  if (state === 'AwaitingStealCardPick' && isThief && stealPhase.thiefPickSlots) {
    return (
      <Modal onClose={() => {}} labelledBy="steal-pick-heading">
        <h2 id="steal-pick-heading" className="mb-3 text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
          Pick a card from {stealPhase.victimName}'s {stealPhase.initialStealTargetZone.toLowerCase()}
        </h2>
        <StealPickFan slots={stealPhase.thiefPickSlots} onPick={onCardPick} isPending={isPending} />
      </Modal>
    );
  }

  return (
    <Modal onClose={() => {}} labelledBy="steal-waiting-heading">
      <h2 id="steal-waiting-heading" className="text-lg font-semibold" style={{ color: 'var(--gb-text-primary)' }}>
        {stealPhase.stealingPlayerName} is attempting to steal from {stealPhase.victimName}…
      </h2>
    </Modal>
  );
}

export default StealPrompt;
