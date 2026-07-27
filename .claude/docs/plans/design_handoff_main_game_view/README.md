# Handoff: Trash Pandas — Main Game View (Desktop)

## Overview
Desktop layout for the main gameplay screen of a push-your-luck card/dice game ("Trash Pandas"). Shows the player's own turn area (dice roll, token tray, hand, stash), three opponent summary tiles, a scrollable game log, and several modals (opponent detail, own face-up stash, discard carousel). Includes a day/night theme toggle and an animated "your turn" trash-bag icon.

## About the Design Files
The file in this bundle (`mainView_desktop.html`, flattened from a `.dc.html` design-tool source) is a **design reference built in HTML/CSS/JS** — a prototype showing intended look, layout, and interaction, not production code to copy verbatim. Recreate this design in the target codebase's existing environment (its component framework, state management, and styling approach) using its established patterns. If no environment exists yet, choose the framework best suited to the project and implement there.

## Fidelity
**High-fidelity.** Colors, typography, spacing, sizes, and interaction states below are final — implement pixel-close using the codebase's own component/styling system (don't just embed the HTML).

## Canvas
Fixed design canvas: **1920×1080**, absolutely-positioned layers over a full-bleed background image. If the target app is responsive, treat 1920×1080 as the reference frame and scale/adapt proportionally — ask the team for their intended breakpoint strategy if none exists.

## Screens / Views
Single screen with a Roll/Resolve turn loop and 4 modal overlays.

### Base layer
- Full-bleed background: day image cross-fades to night image on theme toggle (`opacity` transition, 0.6s). A tint overlay (`rgba(10,20,40,0.08)` day / `rgba(5,10,30,0.4)` night) darkens for legibility.
- Font: **Fredoka** (500/600/700), Google Fonts. Base background color `#0b1220`, base page background `#274b6d`.

### Theme toggle (top-right)
- 60×60 circle button, `top:24px; right:24px`. Background `rgba(10,16,32,.55)`, blur(6px), border `1px solid rgba(255,255,255,.18)`.
- Inner 26×26 circle swaps between sun (`radial-gradient(circle,#ffe27a,#f2b134)`, glow `0 0 14px 4px rgba(255,210,90,.7)`) and moon (`#e8eef7`, inset shadow crescent `inset 8px -4px 0 3px #0a1020`).

### Turn indicator (top-center)
- Pill, `top:24px`, centered. Background `rgba(10,16,32,.6)`, blur(6px), border `1px solid rgba(255,255,255,.18)`, radius 999px, padding `12px 26px`.
- Green pulsing dot (10×10, `#7CE38B`, scale 1→1.5 opacity 1→0.4, 1.6s loop) + label text (`YOUR TURN` / `<NAME>'S TURN`), `#f5efe1`, 600 weight, 20px, letter-spacing .06em.
- When it's the player's turn: animated **trash-bag icon** (see Shared Component below) appended after the label.

### Phase toggle ("Rolling"/"Resolving") — only visible on player's own turn
- Positioned `top:90px`, centered, below the turn indicator.
- Segmented pill, 2 equal-width (110px) segments, divided by a 1px `rgba(255,255,255,.25)` line, `overflow:hidden`, rounded pill, same glass background as turn indicator.
- Each segment: 12px, 700 weight, letter-spacing .12em, centered text, `padding:8px 0`.
- **Active state**: "ROLLING" segment gets bg `#7CE38B` / text `#0d2a12` while the player's token tray still has empty slots. Once a filled-but-unresolved token exists, "RESOLVING" becomes active: bg `#f2b134` / text `#3a2306`. Inactive segment: transparent bg, `#cfd8e8` text. Background/color transition 0.25s ease.

### Opponent rail (left side, 3 tiles stacked)
- Container `left:28px; top:110px; width:236px`, vertical stack, 16px gap.
- Each tile: glass card (`rgba(10,16,32,.55)`, blur, border `rgba(255,255,255,.15)`, radius 16px, padding 14px), hover state lightens bg to `rgba(255,255,255,.12)` and border to `rgba(255,255,255,.35)` (0.15s). Cursor pointer — click opens that opponent's detail modal.
- Row 1: 38×38 initial avatar (bg = opponent's assigned color, `#1a1a1a` bold initial letter) + name (`#f5efe1`, 600, 16px) + **trash-bag icon** (shared component) shown only when it's that opponent's turn.
- Row 2: two pill stat badges, `HAND` count and `STASH` count (bg `rgba(255,255,255,.1)`, border `rgba(255,255,255,.18)`, text `#f5efe1` 11px/700, radius 999px, padding `3px 9px`).
- Row 3: 6 small (26×26) circular token slots — filled slots show a cropped token image at full opacity; empty/used slots are dimmed (see Token Slot States below).

### Shared component: animated trash-bag ("your turn") icon
34×38px box, reused identically in the turn indicator and each opponent tile:
- 3 stink "~" wisps (green `#9DEF7F`, 9px, bold) rising and fading in a staggered sequence (`stink-rise` keyframes: translateY 0→-14px, scaleX 1→1.2→1, opacity .85→.4→0; 1.6s loop, each wisp delayed 0/.4s/.8s).
- Trash bag body: 26×28 rounded shape (`border-radius:50% 50% 42% 42%/60% 60% 40% 40%`), color `#2f333b`, inset shadow for volume, plus a small tied-top nub (8×6, same color).
- Backing disc behind the bag: radial gradient `rgba(20,10,0,.55)→rgba(20,10,0,.35)→transparent`, plus a glowing golden ring `box-shadow: 0 0 0 2px rgba(242,177,52,.65), 0 0 14px 4px rgba(242,177,52,.55)` — this ring is what makes the icon read clearly against both light and dark art.
- 2 flies: one orbits the bag in a circle (`fly-orbit`, 2.2s linear, radius 13px), the other moves erratically (`fly-erratic`, 2.9s ease-in-out, ~9-keyframe randomized translate path). Both are small (4-5px) black dots.
- Implementation note: this was built as a standalone reusable component in the design tool — recreate it as one shared component/partial, not copy-pasted markup.

### Player's token tray ("YOUR TOKENS", below the hand)
- Positioned bottom-center, `bottom:24px`, above the hand fan. Glass panel, radius 20px, padding `12px 24px`, shadow `0 8px 20px rgba(0,0,0,.35)`.
- Label "YOUR TOKENS" centered above, `#cfd8e8`, 12px/600, letter-spacing .12em.
- 6 circular slots, 64×64, 14px gap. See **Token Slot States** below.
- **Busted overlay**: if the player has rolled the same token value twice in the current (unresolved) tray, a "BUSTED" stamp image is centered over the entire tray (absolute, `inset:0`, flex-centered, `pointer-events:none`, size ~275×162, `object-fit:contain` so the diagonal stamp graphic isn't cropped) and every filled token in the tray is visually dimmed the same way a "used" token is (see below) to signal the tray is void.

### Token Slot States (applies to both player's 64px tray and opponents' 26px trays)
1. **Empty**: transparent bg, `2px dashed rgba(255,255,255,.4)` border, no image.
2. **Filled / active** (rolled this turn, not yet resolved): bg `#1e2536`, border `2px solid #f2b134`, token image visible, pop-in animation (scale .2→1.18→1, opacity 0→1→1, 0.4s ease), drop shadow.
3. **Used / resolved OR busted**: bg `#3a4150`, border `2px solid rgba(255,255,255,.15)`, opacity 0.4, no animation — same dimmed treatment for "already resolved" and "busted this turn" tokens.
- Token images are square crops of icon art, `object-fit:cover`, filling the circular slot edge-to-edge (no padding baked into the source images).

### Deck + Discard (top-center, between phase toggle and hand)
- Vertically centered in the gap between the phase toggle and the hand fan (`top:120px` down to `bottom:510px` from viewport edges, flex `align-items:center`), horizontally centered, 64px gap between the two piles.
- Each pile: 198px-wide card (aspect ratio 5:7) built from 2-3 stacked card-back images offset by a few px (top/left 0, 4, 9) to suggest pile thickness, rounded 14px, drop shadow. A count badge (44×44 circle, bold number, 2px dark border, drop shadow) sits at bottom-right, overlapping by 15px.
  - Deck badge: `#f2b134` bg, `#2a1a05` text.
  - Discard badge: `#e0533d` bg, `#2a1a05` text.
- Discard pile: top card shows the actual top-of-pile art (not a back); clicking opens the discard carousel modal. Hover scales the whole pile to **1.16x** (signals it's clickable) with 0.18s ease transition.
- Deck: not clickable/interactive in this mock (no hover-scale) — draws happen automatically via game logic.
- Label beneath each pile: "DECK" / "DISCARD", `#cfd8e8`, 13px/600, letter-spacing .08em, text-shadow for legibility over the art.

### Player's stash (bottom-left)
- `left:60px; bottom:64px`, vertical stack, 10px gap.
- Header row: "YOUR STASH" label + a total-count pill badge (`{{count}} TOTAL`, dark bg `rgba(8,14,28,.7)`, border `rgba(255,255,255,.25)`, `#f5efe1` text).
- Below: two 180px-wide (aspect 5:7) piles side by side, 21px gap — **face-down** (left, badge `#f2b134`/`#2a1a05`, not clickable) and **face-up** (right, badge `#7CE38B`/`#0d2a12`, clickable, opens "Your Face-Up Stash" modal, hover scale 1.16x same as discard).
- Face-up pile shows the actual top card image; face-down pile shows only card backs.

### Player's hand (bottom-center, fanned)
- Container `bottom:190px` (raised to clear the token tray below it), centered, `width:1050px; height:320px`.
- Cards (198px wide, aspect 5:7) are laid out with a base rotation and vertical lift proportional to distance from the center card, producing a fan. Default (no hover): tighter spacing (90px card-to-card), subtle rotation (±4°/card-offset), subtle lift.
- **On hover of any card**: whole hand fans out wider (177px spacing) and rotation increases (±8°/offset) and lift increases (21px/offset — an easing/spread effect, not just the hovered card moving).
- **The specific hovered card**: additionally scales to **1.16x**, lifts an extra -34px, and raises to the top of the stacking order (z-index 100) so it doesn't get clipped by neighbors.
- Transition: `left .25s ease, transform .2s ease`.

### Game log (top-right)
- `right:28px; top:110px`, extends down to `bottom:523px` (fixed height region matched to the opponent rail's vertical extent). Glass panel matching opponent tile styling, `width:260px`.
- Header "GAME LOG" (`#cfd8e8`, 13px/600, letter-spacing .12em).
- Scrollable list (`overflow-y:auto`), rendered newest-first using `flex-direction:column-reverse` over a chronologically-ordered array (so new entries appear at the top without re-sorting). Each entry: colored dot (8px, matches the actor's assigned color) + two-line text block (message `#e8ecf4` 13px, timestamp `#7c88a9` 11px).

### Roll / Stop controls (bottom-right)
- `right:80px; bottom:60px`, vertical stack, 20px gap.
- **Roll button**: 120×88 rounded rect, gradient `linear-gradient(160deg,#ffd873,#f2b134)`, 3px border `#a86e12`, "pressed key" shadow (`0 8px 0 #a86e12` + drop shadow). Contains a 34×34 die face (3×3 dot grid, showing a fixed 5-pip pattern in this mock) and a label that reads "ROLL" normally, "NEW TURN" once the tray is completely full.
- **Stop button**: 104×104 hexagon (`clip-path: polygon(30% 0,70% 0,100% 30%,100% 70%,70% 100%,30% 100%,0 70%,0 30%)`). Active (there's at least one filled-unresolved token): bg `#e0533d`, opacity 1, pointer cursor. Inactive: bg `#5a6270`, opacity 0.5, default cursor (visually and functionally disabled).

## Modals
All modals: full-screen scrim `rgba(5,10,20,.65-.7)` + `backdrop-filter: blur(3px)`, click on scrim closes, click inside the modal card is stopped from closing it (`stopPropagation`). Modal card: `rgba(18,26,46,.95)` bg, border `rgba(255,255,255,.18)`, radius 20px, big drop shadow.

### Opponent detail modal
- Opens on opponent-tile click. Has prev/next chevron buttons *outside* the modal card (52px circles) to cycle between the 3 opponents without closing.
- Header: avatar + name + close (✕) button.
- Two stat tiles side by side: **face-down stash count** (`#f2b134`) and **cards in hand** (`#7CE38B`), each large number over a small caption.
- "FACE-UP STASH" section: label + total-count pill, then a wrapping row of face-up cards **grouped by card type with a count badge**, sorted **highest count first**.

### Player's own face-up stash modal
- Same visual pattern as the opponent modal's face-up section (grouped, count-badged, sorted desc) but as its own modal, titled "Your Face-Up Stash", opened by clicking the player's face-up stash pile.

### Discard carousel modal
- Header: "DISCARD PILE" label + close button.
- 3-card carousel: prev/next chevrons (52px circles) flanking a row of 3 cards — center card full size (260px wide, opacity 1, scale 1) and the two neighbors smaller (170px, opacity .55, scale .9) — with a position readout below ("`n / total`"). All transitions 0.25s ease.

## Interactions & Behavior Summary
- **Roll**: fills the next empty slot in the player's token tray with a random token type. If the tray is already full, pressing again ("NEW TURN") resets it to 6 empty slots.
- **Bust detection**: if two unresolved slots in the tray end up with the same token type, the tray is "busted" — dim all its tokens and show the BUSTED stamp. (In this mock, bust is derived reactively from tray state; the real game should presumably also auto-clear the tray / end the turn on bust — confirm exact bust behavior with game design, as this mock only handles the *visual* state.)
- **Stop**: marks all filled tokens as "used" (resolved into the stash) and advances `currentTurn` to the next player. Disabled if there's nothing filled-and-unresolved.
- **Turn cycling**: `currentTurn` is a simple index (0 = player, 1..3 = opponents) that round-robins on Stop. Opponent turns in this mock don't run any AI — only the "your turn" trash-icon marker moves to their tile.
- Hover-to-enlarge (1.16x) is used consistently as the "this is clickable" affordance: hand cards, discard pile, own face-up stash pile. Opponent tiles show pointer cursor but do **not** scale (they're clickable to open a modal, but visually static).
- Day/night theme toggle cross-fades backgrounds and tint overlay (0.6s) and swaps the toggle knob between sun/moon styling (0.4s).

## State Management
Minimal state needed to reproduce this screen:
- `theme`: 'day' | 'night'
- `selfTray`: array of 6 slots, each `{ filled: bool, used: bool, tokenId: string|null }`
- `currentTurn`: int (0 = self, 1..N = opponent index + 1)
- `hoveredHandIndex`: int|null (drives hand fan spread + active card scale)
- `selectedOpponent`: int|null (which opponent-detail modal is open, also drives its prev/next)
- `discardOpen`: bool, `discardIndex`: int (carousel position)
- `selfStashOpen`: bool
- Derived (computed each render, not stored): `allFilled`, `hasUnusedFilled` (drives ROLLING vs RESOLVING + Stop button enabled state), `isBusted` (duplicate tokenId among filled-unused slots)
- Static/reference data (would come from real game state in production): `opponents` list (name, color, initial, token tray pattern, face-up cards, face-down count, hand count), `gameLog` entries, `discardPile` order, player's `selfFaceUpCards`, stash counts, deck count.

## Design Tokens

### Colors
- Backgrounds: `#0b1220` (page), `#274b6d` (canvas fallback), glass panels `rgba(10,16,32,.55–.6)`
- Text: `#f5efe1` (primary light), `#cfd8e8` (secondary label), `#e8ecf4` (log text), `#a9b4c6` / `#7c88a9` (muted/timestamps), `#1a1a1a` (on-avatar text)
- Accent green (player / positive / active-roll): `#7CE38B` / dark text pair `#0d2a12`
- Accent gold/amber (active token, deck badge, resolving phase): `#f2b134`, `#ffd873` gradient partner, dark text `#3a2306` / `#2a1a05`
- Accent red (discard/stop/danger): `#e0533d`
- Opponent identity colors (extendable palette): `#9fd8a3` (green), `#f2b6c6` (pink), `#a9c9f2` (blue)
- Stink-wisp green: `#9DEF7F`
- Bag/dark neutral: `#2f333b`, `#3a4150`, `#5a6270`
- Borders/dividers: `rgba(255,255,255,.15–.35)`
- Link color (base reset only, no links in this design): `#ffd166` / hover `#ffe4a1`

### Typography
- Font family: **Fredoka**, weights 500/600/700, sans-serif fallback.
- Scale used: 11px (small badges), 12–13px (labels/captions), 14–16px (names/body), 18–20px (buttons/turn label), 22–26px (modal headings/stat numbers).
- Letter-spacing: .06em–.14em on all-caps labels/buttons.

### Spacing / Radius / Shadow
- Glass panel radius: 16–20px. Pills/badges: 999px (full round). Cards: 10–16px.
- Standard glass panel shadow: `0 6px 16px rgba(0,0,0,.3)`; deeper modal shadow: `0 20px 50px rgba(0,0,0,.5)`.
- Hover "clickable" affordance: `transform: scale(1.16)`, 0.18s ease, `transform-origin: center`.
- Card aspect ratio throughout: **5:7**.

### Animations (keyframes)
- `tp-pop`: scale .2→1.18→1, opacity 0→1→1 — new token appears (0.4s ease, once).
- `tp-pulse`: scale 1→1.5, opacity 1→.4 — turn-indicator dot (1.6s ease-in-out infinite).
- `stink-rise`: translateY 0→-7px→-14px, scaleX 1→1.2→1, opacity .85→.4→0 — trash-icon stink wisps (1.6s ease-in-out infinite, staggered).
- `fly-orbit`: rotate 0→360deg with translateX(13px) counter-rotation — circular fly path (2.2s linear infinite).
- `fly-erratic`: 9-keyframe randomized translate path — jittery fly path (2.9s ease-in-out infinite).

## Assets
All under `assets/` in this bundle (copied from `uploads/assets/` in the design tool):
- `images/backgrounds/day_background.webp`, `night_background.webp` — full-bleed scene art.
- `images/cards/cropped_back.png` — card back, pre-cropped to bleed edge-to-edge (no source padding).
- `images/cards/*.webp` — individual card face art (nanners, feesh, blammo, kitteh, doggo, mmmPie, shiny, yumYum), same edge-to-edge cropping.
- `images/tokens/cropped_*_token.png` — 6 token icons (bandit, doubleStash, doubleTrash, recycle, stashTrash, steal), pre-cropped to fill their circular slot.
- `busted.png` — the "BUSTED" red stamp graphic used in the bust overlay (transparent background, diagonal stamp art — do not re-crop, it's designed to be shown with `object-fit: contain`).

No icon font or SVG icon set is used — all iconography here is either raster art (tokens, cards, the busted stamp) or hand-built with CSS (the trash-bag icon, chevrons via ‹ › glyphs, close via ✕ glyph).

## Files
- `mainView_desktop.html` — the full screen, flattened to plain HTML/CSS/JS for reference (state/logic lives in a `<script>` block at the bottom; treat it as pseudocode for the interaction logic, not literal code to paste in).
- `assets/` — all image assets listed above, preserving the original relative paths used inside the HTML file's `src` attributes.
