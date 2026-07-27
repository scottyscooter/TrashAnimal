import type { CardName, TokenAction } from '../../api/types';

import dayBackground from '../../assets/images/backgrounds/day_background.webp';
import nightBackground from '../../assets/images/backgrounds/night_background.webp';

import cardBack from '../../assets/images/cards/back.png';
import blammoCard from '../../assets/images/cards/blammo.webp';
import doggoCard from '../../assets/images/cards/doggo.webp';
import feeshCard from '../../assets/images/cards/feesh.webp';
import kittehCard from '../../assets/images/cards/kitteh.webp';
import mmmPieCard from '../../assets/images/cards/mmmPie.webp';
import nannersCard from '../../assets/images/cards/nanners.webp';
import shinyCard from '../../assets/images/cards/shiny.webp';
import yumYumCard from '../../assets/images/cards/yumYum.webp';

// .png, not .webp — these are the pre-cropped, edge-to-edge variants (the plain *_token.webp
// files have substantial transparent padding around the icon and were never meant to be used
// directly in a circular slot; see the design handoff's cropped_*_token.png originals).
import banditToken from '../../assets/images/tokens/bandit_token.png';
import doubleStashToken from '../../assets/images/tokens/doubleStash_token.png';
import doubleTrashToken from '../../assets/images/tokens/doubleTrash_token.png';
import recycleToken from '../../assets/images/tokens/recycle_token.png';
import stashTrashToken from '../../assets/images/tokens/stashTrash_token.png';
import stealToken from '../../assets/images/tokens/steal_token.png';

import bustedStamp from '../../assets/icons/busted.png';

export const DAY_BACKGROUND_IMAGE = dayBackground;
export const NIGHT_BACKGROUND_IMAGE = nightBackground;
export const CARD_BACK_IMAGE = cardBack;
export const BUSTED_STAMP_IMAGE = bustedStamp;

// Filenames use inconsistent snake_case/camelCase (mmmPie.webp, yumYum.webp, bandit_token.webp,
// doubleStash_token.webp) that doesn't map onto the CardName/TokenAction enum values by any
// mechanical transform, so these lookups are spelled out explicitly rather than derived.
export const CARD_IMAGE_BY_NAME: Record<CardName, string> = {
  Blammo: blammoCard,
  Nanners: nannersCard,
  Feesh: feeshCard,
  Shiny: shinyCard,
  Yumyum: yumYumCard,
  MmmPie: mmmPieCard,
  Kitteh: kittehCard,
  Doggo: doggoCard,
};

export const TOKEN_IMAGE_BY_ACTION: Record<TokenAction, string> = {
  StashTrash: stashTrashToken,
  DoubleStash: doubleStashToken,
  DoubleTrash: doubleTrashToken,
  Bandit: banditToken,
  Steal: stealToken,
  Recycle: recycleToken,
};

/** Deterministic, client-side-only avatar color assignment for opponent tiles (no color data
 * exists on the backend — seat index is stable for the life of a game, so this is enough). */
export const OPPONENT_COLOR_PALETTE = ['#9fd8a3', '#f2b6c6', '#a9c9f2'] as const;

export function opponentColorForSeat(seatIndex: number): string {
  return OPPONENT_COLOR_PALETTE[seatIndex % OPPONENT_COLOR_PALETTE.length];
}
