import type { GameState } from '../../api/types';

/** GameState values representing "resolving" the current roll/turn rather than actively rolling —
 * shared between PhaseToggle (desktop/tablet's Rolling/Resolving pill) and TurnIndicator (phone
 * landscape's folded-in subtitle, see the mobile landscape plan's Round 2 Finding 1), so the same
 * state -> display mapping isn't duplicated across the two components. */
export const RESOLVING_STATES: GameState[] = ['TokenPhase', 'TurnEnd'];
