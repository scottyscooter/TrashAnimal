import { useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useGameClientIdentity } from '../hooks/useGameClientIdentity'
import { useGameView } from '../hooks/useGameView'
import { useGameSignalR } from '../hooks/useGameSignalR'
import { useGameLogAnnouncements } from '../hooks/useGameLogAnnouncements'
import { useSubmitCommand } from '../hooks/useSubmitCommand'
import { useToast } from '../components/Toast/useToast'
import type { GameAction, HandCardView, TokenAction } from '../api/types'
import { TOKEN_IMAGE_BY_ACTION } from './GameBoard/assetMaps'
import DayNightBackground from '../components/gameboard/DayNightBackground'
import GameBoardThemeToggle from '../components/gameboard/GameBoardThemeToggle'
import TurnIndicator from '../components/gameboard/TurnIndicator'
import PhaseToggle from '../components/gameboard/PhaseToggle'
import OpponentRail from '../components/gameboard/OpponentRail'
import OpponentIndexTabs from '../components/gameboard/OpponentIndexTabs'
import DeckDiscardPiles from '../components/gameboard/DeckDiscardPiles'
import PlayerStash from '../components/gameboard/PlayerStash'
import PlayerHand from '../components/gameboard/PlayerHand'
import TokenTray from '../components/gameboard/TokenTray'
import RollStopControls from '../components/gameboard/RollStopControls'
import YumYumPrompt from '../components/gameboard/YumYumPrompt'
import StealPrompt from '../components/gameboard/StealPrompt'
import VictimPicker from '../components/gameboard/VictimPicker'
import FeeshCardPicker from '../components/gameboard/FeeshCardPicker'
import TokenPhasePanel from '../components/gameboard/TokenPhasePanel'
import BanditResponseModal from '../components/gameboard/BanditResponseModal'
import BanditWaitingModal from '../components/gameboard/BanditWaitingModal'
import GlassPanel from '../components/gameboard/GlassPanel'
import GameLogPanel from '../components/gameboard/GameLogPanel'

type VictimPickerMode = 'shiny' | 'steal' | null

function GameBoardPage() {
  const { gameId } = useParams()
  const { identity } = useGameClientIdentity(gameId)
  const { showToast } = useToast()

  const [victimPickerMode, setVictimPickerMode] = useState<VictimPickerMode>(null)
  const [feeshPickerOpen, setFeeshPickerOpen] = useState(false)

  const gameViewQuery = useGameView(gameId ?? '', identity?.seatIndex ?? -1)
  useGameSignalR(gameId ?? '', identity?.seatIndex ?? -1)
  useGameLogAnnouncements(gameViewQuery.data?.view.log ?? [], identity?.seatIndex ?? -1)
  const submitCommand = useSubmitCommand(gameId ?? '', identity?.seatIndex ?? -1)

  // Tokens only appear in the tray when earned, so the browser won't have fetched them yet
  // on the roll that first produces each token type. Prefetch all six on mount so they're
  // already cached whenever the tray renders them.
  useEffect(() => {
    Object.values(TOKEN_IMAGE_BY_ACTION).forEach((src) => {
      const img = new Image()
      img.src = src
    })
  }, [])

  if (!gameId) {
    return null
  }

  if (!identity) {
    return (
      <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
        <h1>Game Board</h1>
        <p role="alert">Could not find your seat for this game. Try re-joining from the lobby.</p>
      </section>
    )
  }

  if (gameViewQuery.isLoading) {
    return (
      <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
        <h1>Game Board</h1>
        <p>Loading game…</p>
      </section>
    )
  }

  if (gameViewQuery.isError || !gameViewQuery.data) {
    return (
      <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
        <h1>Game Board</h1>
        <p role="alert">This game could not be found.</p>
      </section>
    )
  }

  const { view: gameView, allowedActions } = gameViewQuery.data
  const gameLog = gameView.log
  const localSeatIndex = identity.seatIndex
  const isLocalPlayerTurn = gameView.currentPlayerIndex === localSeatIndex
  const isPending = submitCommand.isPending

  function dispatch(request: Parameters<typeof submitCommand.mutate>[0]) {
    submitCommand.mutate(request, {
      onSuccess: (response) => {
        if (!response.succeeded) {
          showToast(response.errorMessage ?? 'That action was rejected.')
        } else if (response.infoMessage) {
          showToast(response.infoMessage, 'info')
        }
      },
    })
  }

  function handleAction(action: GameAction) {
    dispatch({ kind: 'action', playerSeat: localSeatIndex, action })
  }

  function handleCardPick(cardId: string) {
    dispatch({ kind: 'cardPick', playerSeat: localSeatIndex, cardId })
  }

  function handleDoubleStashSubmit(cardIds: string[]) {
    dispatch({ kind: 'doubleStash', playerSeat: localSeatIndex, cardIds })
  }

  function handleRecyclePick(replacement: TokenAction) {
    dispatch({ kind: 'recyclePick', playerSeat: localSeatIndex, replacement })
  }

  function handleFeeshPick(cardId: string) {
    dispatch({ kind: 'playFeesh', playerSeat: localSeatIndex, cardId })
    setFeeshPickerOpen(false)
  }

  // Routes a hand card's own PlayableAs verdict (per HandCardView's ranked-reason contract) to the
  // right handler — some actions need a follow-up picker (Feesh's discard choice, Shiny's victim
  // choice), others are plain single-shot actions. Only ever called with a non-null playableAs;
  // PlayerHand does not invoke this for unplayable cards.
  function handleHandCardActivate(card: HandCardView) {
    const action = card.playableAs
    if (!action) {
      return
    }

    switch (action) {
      case 'PlayFeesh':
      case 'PlayFeeshTokenPhase':
        setFeeshPickerOpen(true)
        break
      case 'PlayShiny':
      case 'PlayShinyTokenPhase':
        setVictimPickerMode('shiny')
        break
      default:
        handleAction(action)
    }
  }

  function handleVictimPick(victimSeat: number) {
    if (victimPickerMode === 'shiny') {
      dispatch({ kind: 'playShiny', playerSeat: localSeatIndex, victimSeat })
    } else if (victimPickerMode === 'steal') {
      dispatch({ kind: 'resolveTokenSteal', playerSeat: localSeatIndex, victimSeat })
    }
    setVictimPickerMode(null)
  }

  function handleStartSteal() {
    const opponentsWithCards = gameView.opponents.filter((opponent) => opponent.handCount > 0)
    if (opponentsWithCards.length > 0) {
      setVictimPickerMode('steal')
    } else {
      dispatch({ kind: 'resolveTokenSteal', playerSeat: localSeatIndex, victimSeat: null })
    }
  }

  const isLocalYumYumResponder =
    gameView.state === 'AwaitingYumYum' && gameView.yumYumResponderIndex === localSeatIndex

  const isAwaitingBanditResponse = gameView.tokenPhase?.step === 'BanditAwaitOpponentResponse'
  const isLocalBanditResponder =
    isAwaitingBanditResponse && gameView.tokenPhase!.banditCurrentResponderIndex === localSeatIndex

  return (
    <div className="gb-root">
      <DayNightBackground />
      <GameBoardThemeToggle />
      <TurnIndicator currentPlayerName={gameView.currentPlayerName} isLocalPlayerTurn={isLocalPlayerTurn} state={gameView.state} />
      {isLocalPlayerTurn && <PhaseToggle state={gameView.state} />}

      <OpponentRail gameView={gameView} />
      <OpponentIndexTabs gameView={gameView} />
      <div className="fixed right-7 top-[110px] bottom-[523px] z-10 w-[320px]">
        <GameLogPanel entries={gameLog} />
      </div>
      <DeckDiscardPiles deckCount={gameView.deckCount} discardPile={gameView.discardPile} />
      <PlayerStash ownStash={gameView.ownStash} />
      <PlayerHand handCards={gameView.handCards} onCardActivate={handleHandCardActivate} />

      <div className="fixed bottom-6 left-1/2 z-10 -translate-x-1/2 phone-landscape:bottom-[6%]">
        <GlassPanel className="flex flex-col items-center gap-2 rounded-2xl px-6 py-3 phone-landscape:gap-1 phone-landscape:px-3 phone-landscape:py-1.5">
          <span className="text-xs font-semibold tracking-[0.12em] phone-landscape:text-[9px]" style={{ color: 'var(--gb-text-label)' }}>
            YOUR TOKENS
          </span>
          {/* gameView.phaseOneTokens/tokenPhase are single shared fields reflecting whichever
              player is CURRENTLY active, not "my own tokens" — every viewer's own tray must stay
              empty unless it's actually their turn, or everyone sees the active player's rolls
              duplicated into their own panel. The active player's tray is what OpponentTile shows
              to everyone else. */}
          <TokenTray
            phaseOneTokens={isLocalPlayerTurn ? gameView.phaseOneTokens : []}
            tokenPhase={isLocalPlayerTurn ? gameView.tokenPhase : null}
            isBusted={isLocalPlayerTurn && gameView.isBusted}
          />
        </GlassPanel>
      </div>

      {isLocalPlayerTurn && !gameView.tokenPhase && !gameView.stealPhase && !isLocalYumYumResponder && (
        <RollStopControls allowedActions={allowedActions} onAction={handleAction} isPending={isPending} />
      )}

      {isLocalPlayerTurn && allowedActions.includes('PlayShiny') && (
        <button
          type="button"
          disabled={isPending}
          onClick={() => setVictimPickerMode('shiny')}
          className="fixed bottom-[170px] right-[80px] z-20 rounded-lg px-4 py-2 text-sm font-bold disabled:opacity-50 phone-landscape:bottom-[26%] phone-landscape:right-[2%] phone-landscape:px-2 phone-landscape:py-1 phone-landscape:text-[10px]"
          style={{ background: 'var(--gb-gold)', color: 'var(--gb-gold-text)' }}
        >
          Play Shiny
        </button>
      )}

      {isLocalYumYumResponder && (
        <YumYumPrompt allowedActions={allowedActions} onAction={handleAction} isPending={isPending} />
      )}

      {gameView.stealPhase && (
        <StealPrompt
          state={gameView.state}
          stealPhase={gameView.stealPhase}
          localSeatIndex={localSeatIndex}
          allowedActions={allowedActions}
          onAction={handleAction}
          onCardPick={handleCardPick}
          isPending={isPending}
        />
      )}

      {isLocalPlayerTurn && gameView.tokenPhase && !isAwaitingBanditResponse && (
        <TokenPhasePanel
          tokenPhase={gameView.tokenPhase}
          allowedActions={allowedActions}
          isPending={isPending}
          onAction={handleAction}
          onCardPick={handleCardPick}
          onDoubleStashSubmit={handleDoubleStashSubmit}
          onRecyclePick={handleRecyclePick}
          onStartSteal={handleStartSteal}
        />
      )}

      {isLocalPlayerTurn && isAwaitingBanditResponse && <BanditWaitingModal />}

      {isLocalBanditResponder && (
        <BanditResponseModal
          revealedCardName={gameView.tokenPhase!.banditRevealedCardName!}
          stashableCards={gameView.tokenPhase!.stashableHandCardsForCurrentPrompt}
          onStash={handleCardPick}
          onPass={() => handleAction('TokenBanditMatchPass')}
          isPending={isPending}
        />
      )}

      {victimPickerMode && (
        <VictimPicker
          title={victimPickerMode === 'shiny' ? 'Play Shiny — steal from a stash' : 'Steal from a hand'}
          opponents={
            victimPickerMode === 'steal'
              ? gameView.opponents.filter((opponent) => opponent.handCount > 0)
              : gameView.opponents
          }
          onPick={handleVictimPick}
          onClose={() => setVictimPickerMode(null)}
          isPending={isPending}
        />
      )}

      {feeshPickerOpen && (
        <FeeshCardPicker
          discardPile={gameView.discardPile}
          onPick={handleFeeshPick}
          onClose={() => setFeeshPickerOpen(false)}
          isPending={isPending}
        />
      )}
    </div>
  )
}

export default GameBoardPage
