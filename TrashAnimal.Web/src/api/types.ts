// TypeScript mirrors of TrashAnimal.Api's JSON contracts (Contracts/Requests, Contracts/Responses,
// domain GameView/enums, and Updates/*Envelope). Enums serialize as strings (JsonStringEnumConverter),
// and ASP.NET Core's default JSON naming policy is camelCase, so every field here is camelCase even
// though the C# source is PascalCase.

export type CardName =
  | 'Blammo'
  | 'Nanners'
  | 'Feesh'
  | 'Shiny'
  | 'Yumyum'
  | 'MmmPie'
  | 'Kitteh'
  | 'Doggo';

export type TokenAction = 'StashTrash' | 'DoubleStash' | 'DoubleTrash' | 'Bandit' | 'Steal' | 'Recycle';

export type GameState =
  | 'RollPhase'
  | 'AwaitingYumYum'
  | 'AwaitingStealResponse'
  | 'AwaitingStealCardPick'
  | 'TokenPhase'
  | 'TurnEnd'
  | 'GameEnded';

export type TokenPhaseStep =
  | 'ChoosingNextToken'
  | 'StashTrashChooseBranch'
  | 'StashTrashPickCard'
  | 'DoubleStashChoosingCards'
  | 'BanditAwaitOpponentResponse'
  | 'RecycleChoosingReplacement';

export type StealTargetZone = 'Hand' | 'Stash';

export type GameAction =
  | 'RollDie'
  | 'StopRolling'
  | 'AdvanceToResolveTokens'
  | 'PlayShiny'
  | 'PlayFeesh'
  | 'PlayNanners'
  | 'PlayBlammo'
  | 'AbandonBust'
  | 'YumYumPlay'
  | 'YumYumPass'
  | 'StealPass'
  | 'StealPlayDoggo'
  | 'StealPlayKitteh'
  | 'PlayMmmPieTokenPhase'
  | 'PlayShinyTokenPhase'
  | 'PlayFeeshTokenPhase'
  | 'ResolveTokenStashTrash'
  | 'ResolveTokenDoubleStash'
  | 'ResolveTokenDoubleTrash'
  | 'ResolveTokenBandit'
  | 'ResolveTokenSteal'
  | 'ResolveTokenRecycle'
  | 'TokenStashTrashDrawOne'
  | 'TokenStashTrashStashMode'
  | 'TokenDoubleStashSubmit'
  | 'TokenBanditMatchPass'
  | 'EndTurn';

export interface StealPickSlot {
  cardId: string;
  thiefFacingLabel: string;
}

export interface StealPhaseView {
  stealingPlayerIndex: number;
  stealingPlayerName: string;
  victimIndex: number;
  victimName: string;
  initialStealTargetZone: StealTargetZone;
  thiefPickSlots: StealPickSlot[] | null;
}

export interface StashableHandCard {
  cardId: string;
  name: CardName;
}

export interface TokenPhaseView {
  step: TokenPhaseStep;
  remainingTokens: TokenAction[];
  activeToken: TokenAction | null;
  banditRevealedCardName: CardName | null;
  banditCurrentResponderIndex: number | null;
  stashableHandCardsForCurrentPrompt: StashableHandCard[];
  recycleReplacementOptions: TokenAction[];
}

export interface GameView {
  state: GameState;
  currentPlayerIndex: number;
  currentPlayerName: string;
  isBusted: boolean;
  forcedRollRemaining: boolean;
  phaseOneTokens: TokenAction[];
  handCardNames: CardName[];
  yumYumResponderIndex: number | null;
  yumYumResponderName: string | null;
  stealPhase: StealPhaseView | null;
  tokenPhase: TokenPhaseView | null;
}

export interface GameEndScoreLine {
  playerIndex: number;
  playerName: string;
  totalScore: number;
}

// ---- Requests ----

export interface CreateGameRequest {
  playerNames: string[];
  dieSeed?: number | null;
}

export interface CreateLobbyRequest {
  nickname: string;
}

export interface JoinLobbyRequest {
  nickname: string;
}

export interface StartLobbyRequest {
  clientToken: string;
}

/**
 * Wire shape accepted by POST /games/{gameId}/commands. GamesController's dispatcher checks
 * fields in this order: recycleReplacement, then cardIds, then action === 'PlayFeesh'/'PlayShiny'/
 * 'ResolveTokenSteal', then a bare cardId (routed by current GameState/TokenPhaseStep), else the
 * plain action. Prefer building requests via the `SubmitCommandRequest` factories below rather than
 * this raw shape, so the discriminated union in `gamesApi.ts` catches field mistakes at compile time.
 */
export interface SubmitCommandRequestWire {
  playerSeat: number;
  action: GameAction;
  cardId?: string | null;
  cardIds?: string[] | null;
  recycleReplacement?: TokenAction | null;
  victimSeat?: number | null;
}

/**
 * The three GameAction values GamesController special-cases by requiring an extra field.
 * All other actions take no payload.
 */
export type PlainGameAction = Exclude<GameAction, 'PlayFeesh' | 'PlayShiny' | 'ResolveTokenSteal'>;

/**
 * Discriminated union modeling every distinct shape POST /games/{gameId}/commands accepts.
 * `kind: 'action'` covers plain GameAction submissions (RollDie, EndTurn, ...). The remaining
 * variants cover the contextual, GameState/TokenPhaseStep-driven requests the backend routes
 * independently of the `action` field (steal/stash-trash/bandit card picks, double stash, recycle
 * pick) — see review note 2 in the plan doc. Construct these via `gamesApi.ts`'s helpers, which
 * translate each variant into the wire shape above.
 */
export type SubmitCommandRequest =
  | { kind: 'action'; playerSeat: number; action: PlainGameAction }
  | { kind: 'playFeesh'; playerSeat: number; cardId: string }
  | { kind: 'playShiny'; playerSeat: number; victimSeat: number }
  | { kind: 'resolveTokenSteal'; playerSeat: number; victimSeat: number }
  | { kind: 'stealCardPick'; playerSeat: number; cardId: string }
  | { kind: 'stashTrashCardPick'; playerSeat: number; cardId: string }
  | { kind: 'banditStashCardPick'; playerSeat: number; cardId: string }
  | { kind: 'doubleStashSubmit'; playerSeat: number; cardIds: string[] }
  | { kind: 'recyclePick'; playerSeat: number; recycleReplacement: TokenAction };

// ---- Responses ----

export interface GameCreationResponse {
  gameId: string;
  view: GameView;
  allowedActions: GameAction[];
}

export interface PlayerViewResponse {
  view: GameView;
  allowedActions: GameAction[];
  revision: number;
}

export interface GameCommandResponse {
  succeeded: boolean;
  errorMessage: string | null;
  view: GameView | null;
  allowedActions: GameAction[] | null;
}

export interface GameResultResponse {
  scoreLines: GameEndScoreLine[];
  winningPlayerIndex: number;
}

export interface LobbySeatView {
  seatIndex: number;
  nickname: string;
}

export interface LobbyView {
  lobbyId: string;
  seats: LobbySeatView[];
  isStarted: boolean;
  gameId: string | null;
}

export interface LobbyJoinResponse {
  lobby: LobbyView;
  seatIndex: number;
  clientToken: string;
}

export interface LobbyStartResponse {
  gameId: string;
}

// ---- SignalR push payloads ----

export interface GameUpdateEnvelope {
  gameId: string;
  revision: number;
  actingPlayerSeat: number;
  currentGameState: GameState;
}

export interface LobbyUpdateEnvelope {
  lobbyId: string;
  revision: number;
  lobby: LobbyView;
}
