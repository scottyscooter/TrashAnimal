import type { GameAction, TokenAction, TokenPhaseView } from '../../api/types';
import { TOKEN_IMAGE_BY_ACTION } from '../../pages/GameBoard/assetMaps';
import GlassPanel from './GlassPanel';
import GroupedCardPicker from './GroupedCardPicker';

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
  const isChoosingBranchStep = tokenPhase.step === 'StashTrashChooseBranch';
  // On phone landscape, vertically center this step in the empty gap between the turn indicator
  // and the visible hand cards, rather than screen-center (which would drift toward whichever of
  // those two is closer as viewport height changes). `--gb-turn-indicator-bottom` /
  // `--gb-hand-visible-top` are the two anchor tokens for that gap, defined once in index.css —
  // see the comment there (and TurnIndicator.tsx/PlayerHand.tsx's pointers back to it) before
  // changing either side's layout, since these values are hand-derived from those components'
  // classes and don't update themselves. `-translate-y-1/2` centers the panel's own box (not just
  // its top edge) on the computed midpoint. Desktop has ample vertical room, so it keeps the
  // original bottom-[520px] placement shared with every other TokenPhasePanel step.
  const positionClassName = isChoosingBranchStep
    ? 'bottom-[520px] phone-landscape:top-[calc((var(--gb-turn-indicator-bottom)_+_var(--gb-hand-visible-top))_/_2)] phone-landscape:bottom-auto phone-landscape:-translate-y-1/2'
    : 'bottom-[520px] phone-landscape:top-[70px] phone-landscape:bottom-auto';

  return (
    <GlassPanel
      className={`fixed left-1/2 z-20 flex w-[520px] -translate-x-1/2 flex-col gap-3 rounded-2xl p-5 phone-landscape:w-[85%] phone-landscape:max-w-[450px] phone-landscape:p-3 phone-landscape:gap-2 ${positionClassName}`}
    >
      {tokenPhase.step === 'ChoosingNextToken' && (
        <>
          <p className="text-xs font-semibold tracking-[0.12em] phone-landscape:text-[10px]" style={{ color: 'var(--gb-text-label)' }}>
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
                  className="flex flex-col items-center gap-1 disabled:opacity-50 phone-landscape:gap-0"
                >
                  <img
                    src={TOKEN_IMAGE_BY_ACTION[token]}
                    alt={token}
                    className="h-[52px] w-[52px] rounded-full border-2 object-cover phone-landscape:h-[40px] phone-landscape:w-[40px]"
                    style={{ borderColor: 'var(--gb-gold)' }}
                  />
                  <span className="text-[11px] phone-landscape:text-[8px]" style={{ color: 'var(--gb-text-primary)' }}>
                    {token}
                  </span>
                </button>
              );
            })}
          </div>
        </>
      )}

      {tokenPhase.step === 'StashTrashChooseBranch' && (
        <div className="flex gap-3 phone-landscape:gap-2">
          {allowedActions.includes('TokenStashTrashDrawOne') && (
            <button
              type="button"
              disabled={isPending}
              onClick={() => onAction('TokenStashTrashDrawOne')}
              className="flex-1 rounded-lg py-2 text-sm font-bold disabled:opacity-50 phone-landscape:py-1 phone-landscape:text-xs"
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
              className="flex-1 rounded-lg py-2 text-sm font-bold disabled:opacity-50 phone-landscape:py-1 phone-landscape:text-xs"
              style={{ background: 'var(--gb-green)', color: 'var(--gb-green-text)' }}
            >
              Stash a card
            </button>
          )}
        </div>
      )}

      {tokenPhase.step === 'StashTrashPickCard' && (
        <GroupedCardPicker
          cards={tokenPhase.stashableHandCardsForCurrentPrompt}
          min={1}
          max={1}
          isPending={isPending}
          onConfirm={(ids) => onCardPick(ids[0])}
        />
      )}

      {tokenPhase.step === 'DoubleStashChoosingCards' && (
        <GroupedCardPicker
          cards={tokenPhase.stashableHandCardsForCurrentPrompt}
          min={0}
          max={2}
          isPending={isPending}
          onConfirm={onDoubleStashSubmit}
        />
      )}

      {tokenPhase.step === 'StealChoosingVictim' && (
        <>
          <p className="text-xs font-semibold tracking-[0.12em] phone-landscape:text-[10px]" style={{ color: 'var(--gb-text-label)' }}>
            STEAL AGAIN — CHOOSE A PLAYER
          </p>
          <button
            type="button"
            disabled={isPending}
            onClick={onStartSteal}
            className="self-start rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50 phone-landscape:px-3 phone-landscape:py-1 phone-landscape:text-xs"
            style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
          >
            Choose a player
          </button>
        </>
      )}

      {tokenPhase.step === 'RecycleChoosingReplacement' && (
        <>
          <p className="text-xs font-semibold tracking-[0.12em] phone-landscape:text-[10px]" style={{ color: 'var(--gb-text-label)' }}>
            PICK A REPLACEMENT TOKEN
          </p>
          <div className="flex flex-wrap gap-3 phone-landscape:gap-2">
            {tokenPhase.recycleReplacementOptions.map((token) => (
              <button
                key={token}
                type="button"
                disabled={isPending}
                onClick={() => onRecyclePick(token)}
                className="flex flex-col items-center gap-1 disabled:opacity-50 phone-landscape:gap-0"
              >
                <img
                  src={TOKEN_IMAGE_BY_ACTION[token]}
                  alt={token}
                  className="h-[52px] w-[52px] rounded-full border-2 object-cover phone-landscape:h-[40px] phone-landscape:w-[40px]"
                  style={{ borderColor: 'var(--gb-gold)' }}
                />
                <span className="text-[11px] phone-landscape:text-[8px]" style={{ color: 'var(--gb-text-primary)' }}>
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

export default TokenPhasePanel;
