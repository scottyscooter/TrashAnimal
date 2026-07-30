# Plan B — "Select the Card, Not a Button" Interaction Model

**Source:** `DEBUG_NOTES.md` issues #1, #5, #12, #14
(notes file lives outside the repo, in the Claude scratchpad — not tracked in git)
**Scope:** replace scattered per-card action buttons with a consistent model — the player selects the *card* directly, and the app infers the action. Also gives the player's own stash a viewable listing and rebuilds the multi-select stash modal.
**Sibling plans:** [Plan A — correctness & blocked states](debug-notes-a-correctness-and-blocked-states.md), [Plan C — steal information leak](debug-notes-c-steal-information-leak.md).
**Dependency:** section **A2** of Plan A must land first. B1 and B2 both need `HandCardView.PlayableAs` to know which action a card click should dispatch.

> **⛔ Hard blocker: `fix/token-phase-steal-exhaustion` must land on `main` before any section here starts** (and before Plan A's A2, which this plan depends on). `PlayerHand.tsx`, `TokenPhasePanel.tsx`, and `GameBoardPage.tsx` — the primary files every section here edits — are all currently dirty/uncommitted on that branch, and `GameCommandResult.InfoMessage` (needed by B1) doesn't exist on `main`. See [debug-notes-shared-execution-notes.md](debug-notes-shared-execution-notes.md) for the full evidence.

---

## Execution Model

### Agent assignments

| Section | Owner agent(s) | Notes |
|---|---|---|
| **B0 Hand click routing** | `frontend-engineer` | Small mechanical seam; enables B1/B3. |
| **B1 Bust recovery via card click** | `frontend-engineer` | Removes buttons in `RollStopControls`; relies on B0's routing. Backend `AbandonBust` info-message tweak is trivial and can be added by the same engineer. |
| **B2 Own face-down stash viewer** | `backend-engineer` (contract change on `OwnStashView`), then `ui-designer` for the modal look, then `frontend-engineer` to wire | Only section that clearly benefits from `ui-designer` — a new user-facing surface. |
| **B3 Drop MmmPie button** | `frontend-engineer` | JSX-only deletion after B0. |
| **B4 Grouped card picker** | `ui-designer` for the component shell (visual grammar, `+`/`−` affordance), then `frontend-engineer` to swap the two call sites | New reusable component — worth having the designer own the primitive. |

`backend-engineer` is only needed for B2's `OwnStashView` extension.

### Branch topology

- `feat/hand-playability-routing` — B0 + B3 (both are tiny, both depend on A2; ship together)
- `feat/bust-recovery-via-card` — B1
- `feat/own-stash-visibility` — B2
- `feat/grouped-card-picker` — B4

### Worktree strategy

**High-conflict file:** `TrashAnimal.Web/src/pages/GameBoardPage.tsx` — B0, B1, and B2 all touch it. Serialize them; do not run their worktrees in parallel unless you're willing to eat rebases.
**High-conflict file:** `TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx` — B3 and B4 both edit it. Serialize (B3 is a small delete, do it first).
**Safe parallel:** B2's backend work (`GameView.cs`, `GameSession.cs`) is independent of everything else and can run concurrently with B0/B1/B3/B4 up until the point B2 needs to wire the frontend — at which point the `GameBoardPage.tsx` serialization above applies.

### Conflict-zone map

| File | Sections that touch it |
|---|---|
| `TrashAnimal.Web/src/components/gameboard/PlayerHand.tsx` | B0 |
| `TrashAnimal.Web/src/pages/GameBoardPage.tsx` | **B0 + B1 + B2** — serialize |
| `TrashAnimal.Web/src/components/gameboard/RollStopControls.tsx` | B1 |
| `TrashAnimal.Web/src/components/gameboard/PlayerStash.tsx` | B2 |
| `TrashAnimal/GameView.cs` (`OwnStashView`) | B2 |
| `TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx` | **B3 + B4** — serialize (B3 first) |
| `TrashAnimal.Web/src/components/gameboard/StashModal.tsx` | B2 (mode extension) — no B4 overlap despite the note's title |
| new `TrashAnimal.Web/src/components/gameboard/GroupedCardPicker.tsx` | B4 only |

### Cross-plan coordination

**Blocked on Plan A section A2** — `HandCardView.PlayableAs` and `UnplayableReason` must be merged before any B section starts. B0 routes off `PlayableAs`; B1's Nanners/Blammo removal relies on A2 marking them playable only while busted; B3 relies on A2 marking MmmPie playable only during TokenPhase.

See [debug-notes-shared-execution-notes.md](debug-notes-shared-execution-notes.md) for the cross-plan lock order.

---

## Framing

The notes gather four issues, but the underlying observation is one: the current UI has grown a *button per card-play verb* (`Play MmmPie`, `PLAY NANNERS`, `PLAY BLAMMO`, plus the whole invisible-button-per-card overload of `PlayerHand` from Plan A). That's not how a card game reads. The player thinks *"I'll play the Nanners"* and looks at their hand.

The target model, unified across every surface:

> **A playable card in the hand accepts a click. That click dispatches the one action the card resolves to right now, given context.**

Buttons remain, but only for actions that *aren't* a card play — `STOP`, `ROLL`, `ADVANCE`, `NEW TURN`, `ABANDON`/its replacement, `Stash X cards` confirm, token-tray "resolve this token".

Plan A section A2 already delivers the required per-card verdict (`HandCardView.PlayableAs`). B rides on that.

---

## B0 — Wire the hand click through to `PlayableAs`

Small, mechanical, prerequisite for B1 and B2. Called out separately because it's the seam every subsequent section reuses.

### Work items

1. Change `PlayerHand`'s click model from "always calls `onFeeshClick`" to:
   ```tsx
   onClick={() => card.playableAs && onCardPlay(card)}
   ```
   with `onCardPlay: (card: HandCardView) => void` as a new prop, replacing `onFeeshClick`.
2. In `GameBoardPage`, add a `handleHandCardPlay(card)` that switches on `card.playableAs`:
   - `'PlayFeesh'` → open the `FeeshCardPicker` (existing behavior — no functional change)
   - `'PlayShiny'` → open `victimPickerMode = 'shiny'` (replaces the standalone "Play Shiny" button at [GameBoardPage.tsx:187](TrashAnimal.Web/src/pages/GameBoardPage.tsx:187), which is removed)
   - `'PlayNanners'` / `'PlayBlammo'` → dispatch immediately (B1 removes the buttons)
   - `'PlayMmmPieTokenPhase'` → dispatch immediately (B3 removes the button)
   - `null` → guarded upstream, but assert for safety
3. Remove the "Play Shiny" button from `GameBoardPage` — the click on the Shiny card is now the entry point. Keep the existing `shinyDisabledExplanation` copy but source it from `HandCardView.UnplayableReason` (populated by A2) rather than recomputing in the page.

### Notes

The `title={canPlayFeesh ? ... : card.name}` tooltip at [PlayerHand.tsx:75](TrashAnimal.Web/src/components/gameboard/PlayerHand.tsx:75) becomes wrong once cards mean different things — update it to something like `` `Play ${card.name}` `` when playable, and A2's `unplayableReason` otherwise.

### Tests

- `PlayerHand.test.tsx` — clicking a Feesh card fires the picker path; clicking a Nanners card dispatches `PlayNanners`; clicking an unplayable card fires nothing.

---

## B1 — Issue #5: bust recovery via card click (remove `PLAY NANNERS`/`PLAY BLAMMO` buttons; rename `ABANDON`)

### Verified current state

`RollStopControls` ([RollStopControls.tsx:26–63](TrashAnimal.Web/src/components/gameboard/RollStopControls.tsx:26)) renders three side-buttons whenever the player is busted: `PLAY NANNERS`, `PLAY BLAMMO`, `ABANDON`. The card-play buttons duplicate what the hand already shows.

Per [TrashAnimal/CLAUDE.md](TrashAnimal/CLAUDE.md)'s state-machine section, `AbandonBust` = *"draw 1, skip straight to TurnEnd"*. That's the concrete rule the note asks to reflect in copy.

### Work items

1. Remove the `PlayNanners` and `PlayBlammo` buttons from `RollStopControls`. Bust-recovery card plays now happen via B0 — the busted player clicks the Nanners/Blammo in their hand, `PlayableAs` routes it, done.
2. **A2 must correctly report `PlayableAs: 'PlayNanners'` / `'PlayBlammo'` on those cards *only while busted*.** Add explicit tests for this — it's the hinge the whole B1 change hangs on. If A2 gets it wrong, the buttons come back visibly missing with no fallback.
3. Keep the `ABANDON` button (there's no card to click for it), but relabel per the note:
   - **Recommended:** `DRAW 1 & END TURN`. Matches the actual rule, sets expectations before the click.
   - Keep the `--gb-red` styling — it's still a "give up on this turn" action.
4. Add issue #15's consolation-card message here at the same time, since it's about this exact click: on the success path for `AbandonBust`, surface an `InfoMessage` from the backend — `"Drew 1 card (bust consolation) — turn ended."` — via the same `dispatch` `onSuccess` path used elsewhere. That requires `GameApplicationService`'s `AbandonBust` handling to set an `InfoMessage` (branch-dependent — see Plan A pre-flight for `InfoMessage` availability).

### Tests

- `RollStopControls.test.tsx` — busted + can play Nanners: no `PLAY NANNERS` button rendered; abandon button reads `DRAW 1 & END TURN`.
- Integration (`TrashAnimal.Api.Tests`) — `AbandonBust` command returns an `infoMessage` naming the drawn card.

---

## B3 — Issue #1: remove the MmmPie button from `TokenPhasePanel`

### Verified current state

[TokenPhasePanel.tsx:52–62](TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx:52) renders `Play MmmPie (repeat this token)` whenever `PlayMmmPieTokenPhase` is in the allowed actions, in addition to the MmmPie card sitting in the hand. Same duplication as B1.

### Work items

1. Remove the `PlayMmmPieTokenPhase` button block (lines 52–62 of the current file).
2. A2 already gives MmmPie its `PlayableAs = 'PlayMmmPieTokenPhase'` when the token phase allows it, so B0's hand-click routing dispatches it correctly. Nothing else needed on the panel side.
3. The `onAction` prop on `TokenPhasePanel` no longer needs to handle `'PlayMmmPieTokenPhase'` — the remaining callers (token-tray resolves, StashTrash branch buttons, DoubleStash submit, recycle picks) are all real action buttons, not card plays. Leave `onAction` as-is; just prune the JSX.
4. **Copy note:** since MmmPie now works implicitly, add a tiny one-line hint under the `RESOLVE A TOKEN` header — **"Playing MmmPie allows you to resolve your next token twice."** — so the affordance is discoverable.

### Tests

- `TokenPhasePanel.test.tsx` — the MmmPie button is not rendered even when `PlayMmmPieTokenPhase` is in `allowedActions`; the "resolve this token" buttons still render for `remainingTokens`.

---

## B2 — Issue #12: player can view their own stash cards

### Verified current state

`PlayerStash` ([PlayerStash.tsx](TrashAnimal.Web/src/components/gameboard/PlayerStash.tsx)) already lets the player click the face-up column to open `StashModal` and see face-up cards. **The face-down column shows only a card back and a count** — the player cannot see what *they themselves* stashed face-down, which is what the note asks for.

The domain wire already carries the needed data: `OwnStashView` ([GameView.cs:39](TrashAnimal/GameView.cs:39)) is *`(int FaceDownCount, IReadOnlyList<StashableHandCard> FaceUpCards)`*. Face-down cards' identities are **not** in `OwnStashView`, only the count. Face-up cards *are* fully identified.

That's a gap: the domain doesn't currently expose the viewer's own face-down stash contents even to the viewer. It could — face-down-ness is a public/opponent-facing concept, not a hidden-from-owner one.

### Work items

1. **Domain:** extend `OwnStashView` to carry the viewer's face-down stash contents:
   ```csharp
   public sealed record OwnStashView(
       IReadOnlyList<StashableHandCard> FaceDownCards,   // NEW — replaces FaceDownCount
       IReadOnlyList<StashableHandCard> FaceUpCards);
   ```
   Populate from `Players[viewerIndex].StashPile` in `GameSession.GetViewForPlayer`, splitting by `IsFaceUp`. `FaceDownCount` becomes `ownStash.faceDownCards.length` on the frontend — no data loss.
2. **Contract mirror + tests:** update `src/api/types.ts` `OwnStashView` and `src/api/contracts.test.ts`.
3. **`PlayerStash`:** make the face-down column clickable when `faceDownCards.length > 0`. Open a new modal instance (or a second mode of `StashModal`) titled `"Your Face-Down Stash"`, showing the actual cards face-up in that view — the player already owns the information; the "face-down" state is only what *opponents* see.
4. Keep the current visual affordance (card-back image + count badge on the closed pile) — the click just reveals the contents in a modal rather than surfacing them on the board.

### Hidden-information note

This is intentional: opponents' face-down stash contents remain hidden — `OpponentSummaryView` continues to carry only `StashFaceDownCount`, unchanged. `OwnStashView`'s expansion is scoped to the viewer's *own* pile.

### Tests

- `TrashAnimal.Tests` — `OwnStashView.FaceDownCards` populates from face-down entries only; face-up entries stay in `FaceUpCards`.
- `PlayerStash.test.tsx` — clicking the face-down column with cards present opens a modal listing them by name; the column is disabled when empty.
- Contract test — `OwnStashView`'s new shape.

---

## B4 — Issue #14: stash-modal card selection overhaul (grouped + / − counters)

### Verified current state

The `HandCardPickList` used by `TokenPhaseStep.StashTrashPickCard` ([TokenPhasePanel.tsx:214](TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx:214)) and the inline `DoubleStashChoosingCards` block ([TokenPhasePanel.tsx:127](TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx:127)) both render one clickable thumbnail per card instance. `stashableHandCardsForCurrentPrompt` is a flat `HandCardView[]`, so a hand with 5 YumYum cards renders 5 near-identical tiles — exactly what the note calls out.

There is also a separate `StashModal` component (`StashModal.tsx`), currently read-only — used by `PlayerStash` (B2 above) to display the player's own stash. Do **not** conflate that read-only viewer with the pick-list. The note is about the *pick* control, not the *view* modal, despite the note's title.

### Work items

1. **New component `GroupedCardPicker.tsx`** in `TrashAnimal.Web/src/components/gameboard/`.
   - Props:
     ```ts
     interface GroupedCardPickerProps {
       cards: HandCardView[];            // full pickable pool (may have duplicates)
       min: number;                      // inclusive (0 for DoubleStash, 1 for StashTrash)
       max: number;                      // inclusive (1 for StashTrash, 2 for DoubleStash)
       isPending: boolean;
       onConfirm: (cardIds: string[]) => void;
       confirmLabelPrefix?: string;      // e.g. "Stash" → "Stash 2 cards"
     }
     ```
   - Groups by `card.name`. Renders one enlarged tile per unique name showing card art, name, and `X/Y` (selected / total in hand).
   - `+`/`−` controls per tile; `+` disabled when the tile's count hits its per-card total OR when the overall selection hits `max`; `−` disabled at 0.
   - Header/footer displays `X cards selected` and the confirm button (`Stash X cards`). Confirm disabled when `selected.length < min`.
   - Selection resolves to `cardIds`: when a group has `k` selected, take the first `k` `HandCardView.cardId`s of that group (order in the flat pool). Cards within a group are indistinguishable from the game's perspective, so which specific `cardId`s go doesn't matter — the backend accepts any `k` of them.
2. **Replace usages in `TokenPhasePanel`:**
   - `StashTrashPickCard` block → `<GroupedCardPicker min={1} max={1} onConfirm={ids => onCardPick(ids[0])} ... />`. **Always require the explicit confirm button, even at `min === max === 1` — no auto-submit on `+`.** A player may tap `+` while still deciding and want to change their pick before committing; auto-submit removes that chance to reconsider. This applies uniformly across every `GroupedCardPicker` instance, not just this one.
   - `DoubleStashChoosingCards` block → `<GroupedCardPicker min={0} max={2} onConfirm={onDoubleStashSubmit} ... />`. Delete the inline `doubleStashSelection` state and the inline `<HandCardPickList>` usage — the new component owns both.
3. **`BanditResponseModal.tsx` is explicitly out of scope.** Verified: it does not use `HandCardPickList` or any per-instance tile grid today — it's already a single yes/no text prompt (`"Would you like to stash a {revealedCardName} face-up or pass?"`) that auto-selects `stashableCards[0]` on Stash. There is nothing here for `GroupedCardPicker` to replace. The actual outstanding gap on this modal — showing the card's image so the player has a visual reference instead of just its name — is issue #3 from the original notes (missing card image + hand/stash counts), not covered by this plan. Track it separately if you want it done.
4. **Delete the private `HandCardPickList`** at the bottom of `TokenPhasePanel.tsx` once no callers remain.

### Per-scenario caps to verify

The note enumerates `DoubleStash 0–2`, `StashTrash 0–1`. Actual current caps from the code:
- `StashTrashPickCard` — exactly 1 (`onCardPick` submits immediately on a single click). Model as `min=1, max=1`.
- `DoubleStashChoosingCards` — 0 to 2 (`current.length >= 2` guard at [TokenPhasePanel.tsx:46](TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx:46), submits any 0/1/2). Model as `min=0, max=2`.
- **Verify against the domain** — do not assume the note's numbers are current. Check `TokenPhaseCoordinator` / `TokenPhaseGameActionDispatcher` for the actual accepted counts.

### Tests

- `GroupedCardPicker.test.tsx` — grouping collapses duplicates; `+`/`−` respect per-card and overall caps; confirm produces the right `cardIds`; disabled states line up with `min`/`max`.
- `TokenPhasePanel.test.tsx` — update `DoubleStashChoosingCards` and `StashTrashPickCard` assertions to the new UI.

---

## Suggested execution order

1. **Plan A section A2 must be merged before starting.** No B section can proceed without `HandCardView.PlayableAs`.
2. **B0** — enables B1 and B3.
3. **B1**, **B3** — independent, can run in parallel after B0.
4. **B2** — independent of B0/B1/B3 (only touches `OwnStashView` + `PlayerStash`); can run in parallel with B0 as well.
5. **B4** — independent, but the new component gets used by B1's bust-recovery path only if you decide bust recovery should ever use it (it shouldn't — bust recovery is a click-a-card action, not a multi-select). Safe to do last.

## Open questions for the user

None outstanding. (B1 label confirmed as `DRAW 1 & END TURN`; B3 hint copy set to "Playing MmmPie allows you to resolve your next token twice."; B4 always requires the confirm button, no auto-submit; B4 confirmed `BanditResponseModal` out of scope — its card-image gap is issue #3, untracked by this plan set.)
