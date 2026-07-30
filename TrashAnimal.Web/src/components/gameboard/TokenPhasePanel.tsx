import { useState } from 'react';
import type { GameAction, TokenAction, TokenPhaseView } from '../../api/types';
import { CARD_IMAGE_BY_NAME, TOKEN_IMAGE_BY_ACTION } from '../../pages/GameBoard/assetMaps';
import GlassPanel from './GlassPanel';

const RESOLVE_TOKEN_ACTION: Record<TokenAction, GameAction | null> = {
  StashTrash: 'ResolveTokenStashTrash',
  DoubleStash: 'ResolveTokenDoubleStash',
  DoubleTrash: 'ResolveTokenDoubleTrash',
  Bandit: 'ResolveTokenBandit',
  Recycle: 'ResolveTokenRecycle',
  Steal: null, // handled separately — needs a victimSeat, not a plain action
};

interface TokenPhasePanelProps {
  tokenPhase: TokenPhaseView;
  allowedActions: GameAction[];
  isPending: boolean;
  onAction: (action: GameAction) => void;
  onCardPick: (cardId: string) => void;
  onDoubleStashSubmit: (cardIds: string[]) => void;
  onRecyclePick: (replacement: TokenAction) => void;
  onStartSteal: () => void;
}

/** The "minimal functional UI" for every TokenPhaseStep, as seen by the active player.
 * `BanditAwaitOpponentResponse` has no case here — the responder is never the active player, and
 * the active player instead sees BanditWaitingModal while the (separate) BanditResponseModal
 * handles the actual responder, both rendered independently of whose turn it is in GameBoardPage. */
function TokenPhasePanel({
  tokenPhase,
  allowedActions,
  isPending,
  onAction,
  onCardPick,
  onDoubleStashSubmit,
  onRecyclePick,
  onStartSteal,
}: TokenPhasePanelProps) {
  const [doubleStashSelection, setDoubleStashSelection] = useState<string[]>([]);

  function toggleDoubleStashCard(cardId: string) {
    setDoubleStashSelection((current) => {
      if (current.includes(cardId)) return current.filter((id) => id !== cardId);
      if (current.length >= 2) return current;
      return [...current, cardId];
    });
  }

  return (
    <GlassPanel className="fixed bottom-[520px] left-1/2 z-20 flex w-[520px] -translate-x-1/2 flex-col gap-3 rounded-2xl p-5">
      {allowedActions.includes('PlayMmmPieTokenPhase') && (
        <button
          type="button"
          disabled={isPending}
          onClick={() => onAction('PlayMmmPieTokenPhase')}
          className="self-start rounded-lg px-3 py-1.5 text-xs font-bold disabled:opacity-50"
          style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
        >
          Play MmmPie (repeat this token)
        </button>
      )}

      {tokenPhase.step === 'ChoosingNextToken' && (
        <>
          <p className="text-xs font-semibold tracking-[0.12em]" style={{ color: 'var(--gb-text-label)' }}>
            RESOLVE A TOKEN
          </p>
          <div className="flex flex-wrap gap-3">
            {tokenPhase.remainingTokens.map((token) => {
              const action = RESOLVE_TOKEN_ACTION[token];
              return (
                <button
                  key={token}
                  type="button"
                  disabled={isPending}
                  onClick={() => (token === 'Steal' ? onStartSteal() : action && onAction(action))}
                  className="flex flex-col items-center gap-1 disabled:opacity-50"
                >
                  <img
                    src={TOKEN_IMAGE_BY_ACTION[token]}
                    alt={token}
                    className="h-[52px] w-[52px] rounded-full border-2 object-cover"
                    style={{ borderColor: 'var(--gb-gold)' }}
                  />
                  <span className="text-[11px]" style={{ color: 'var(--gb-text-primary)' }}>
                    {token}
                  </span>
                </button>
              );
            })}
          </div>
        </>
      )}

      {tokenPhase.step === 'StashTrashChooseBranch' && (
        <div className="flex gap-3">
          {allowedActions.includes('TokenStashTrashDrawOne') && (
            <button
              type="button"
              disabled={isPending}
              onClick={() => onAction('TokenStashTrashDrawOne')}
              className="flex-1 rounded-lg py-2 text-sm font-bold disabled:opacity-50"
              style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
            >
              Draw a card
            </button>
          )}
          {allowedActions.includes('TokenStashTrashStashMode') && (
            <button
              type="button"
              disabled={isPending}
              onClick={() => onAction('TokenStashTrashStashMode')}
              className="flex-1 rounded-lg py-2 text-sm font-bold disabled:opacity-50"
              style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
            >
              Stash a card
            </button>
          )}
        </div>
      )}

      {tokenPhase.step === 'StashTrashPickCard' && (
        <HandCardPickList
          cards={tokenPhase.stashableHandCardsForCurrentPrompt}
          isPending={isPending}
          onPick={onCardPick}
        />
      )}

      {tokenPhase.step === 'DoubleStashChoosingCards' && (
        <>
          <p className="text-xs" style={{ color: 'var(--gb-text-label)' }}>
            Pick 0–2 cards to stash.
          </p>
          <div className="flex flex-wrap gap-2">
            {tokenPhase.stashableHandCardsForCurrentPrompt.map((card) => {
              const selected = doubleStashSelection.includes(card.cardId);
              return (
                <button
                  key={card.cardId}
                  type="button"
                  disabled={isPending}
                  onClick={() => toggleDoubleStashCard(card.cardId)}
                  className="relative rounded-lg border-2 disabled:opacity-50"
                  style={{ borderColor: selected ? 'var(--gb-green)' : 'transparent' }}
                >
                  <img
                    src={CARD_IMAGE_BY_NAME[card.name]}
                    alt={card.name}
                    className="h-[84px] w-[60px] rounded-md object-cover"
                  />
                </button>
              );
            })}
          </div>
          <button
            type="button"
            disabled={isPending}
            onClick={() => onDoubleStashSubmit(doubleStashSelection)}
            className="self-start rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
            style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
          >
            Stash {doubleStashSelection.length} card{doubleStashSelection.length === 1 ? '' : 's'}
          </button>
        </>
      )}

      {tokenPhase.step === 'StealChoosingVictim' && (
        <>
          <p className="text-xs font-semibold tracking-[0.12em]" style={{ color: 'var(--gb-text-label)' }}>
            STEAL AGAIN — CHOOSE A PLAYER
          </p>
          <button
            type="button"
            disabled={isPending}
            onClick={onStartSteal}
            className="self-start rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50"
            style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
          >
            Choose a player
          </button>
        </>
      )}

      {tokenPhase.step === 'RecycleChoosingReplacement' && (
        <>
          <p className="text-xs font-semibold tracking-[0.12em]" style={{ color: 'var(--gb-text-label)' }}>
            PICK A REPLACEMENT TOKEN
          </p>
          <div className="flex flex-wrap gap-3">
            {tokenPhase.recycleReplacementOptions.map((token) => (
              <button
                key={token}
                type="button"
                disabled={isPending}
                onClick={() => onRecyclePick(token)}
                className="flex flex-col items-center gap-1 disabled:opacity-50"
              >
                <img
                  src={TOKEN_IMAGE_BY_ACTION[token]}
                  alt={token}
                  className="h-[52px] w-[52px] rounded-full border-2 object-cover"
                  style={{ borderColor: 'var(--gb-gold)' }}
                />
                <span className="text-[11px]" style={{ color: 'var(--gb-text-primary)' }}>
                  {token}
                </span>
              </button>
            ))}
          </div>
        </>
      )}
    </GlassPanel>
  );
}

function HandCardPickList({
  cards,
  isPending,
  onPick,
}: {
  cards: TokenPhaseView['stashableHandCardsForCurrentPrompt'];
  isPending: boolean;
  onPick: (cardId: string) => void;
}) {
  return (
    <div className="flex flex-wrap gap-2">
      {cards.map((card) => (
        <button
          key={card.cardId}
          type="button"
          disabled={isPending}
          onClick={() => onPick(card.cardId)}
          className="transition-transform duration-150 hover:scale-[1.08] disabled:opacity-50"
        >
          <img
            src={CARD_IMAGE_BY_NAME[card.name]}
            alt={card.name}
            className="h-[84px] w-[60px] rounded-md object-cover"
          />
        </button>
      ))}
    </div>
  );
}

export default TokenPhasePanel;
