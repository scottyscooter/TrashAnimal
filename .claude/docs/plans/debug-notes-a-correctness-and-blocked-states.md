# Plan A — Correctness & Blocked States

**Source:** `DEBUG_NOTES.md` issues #2, #6, #16
(notes file lives outside the repo, in the Claude scratchpad — not tracked in git)
**Scope:** three bugs — a game rule the UI never explains, a whole class of "player is acted on but told nothing", and a stuck info bubble.
Issue #8 (steal token with no targets) is already implemented on `fix/token-phase-steal-exhaustion`; it is not covered here and should be verified as part of that branch's merge.
**Sibling plans:** [Plan B — card-selection interaction model](debug-notes-b-card-selection-interaction-model.md), [Plan C — steal information leak](debug-notes-c-steal-information-leak.md). Plan B **depends on section A2 of this document** (the `HandCardView` playability contract). Land A2 first.

> **⛔ Hard blocker: `fix/token-phase-steal-exhaustion` must land on `main` before any section here starts.** Confirmed by inspection, not assumption — `InfoBadge.tsx` (fixed by A4) doesn't exist on `main` at all, and every other file this plan touches is currently dirty/uncommitted on that branch. See [debug-notes-shared-execution-notes.md](debug-notes-shared-execution-notes.md) for the full evidence.

---

## Execution Model

### Agent assignments

| Section | Owner agent(s) | Notes |
|---|---|---|
| **A0 Discovery** | `general-purpose` (read-only sweep) | Produces a checklist consumed by A1. |
| **A1 Announcements class** | `backend-engineer` for the `AffectedPlayerSeat` field + projector work; `frontend-engineer` for the hook + `GameBoardPage` wiring | Sequential: backend first (contract lands), then frontend. |
| **A2 Playability contract** | `backend-engineer` for the domain/contract work; `frontend-engineer` for the `PlayerHand` rendering | Sequential: backend first. This is the critical-path change — don't fan out. |
| **A4 InfoBadge fix** | `frontend-engineer` | Fully self-contained; no `ui-designer` needed (behavioral, not visual). |

`ui-designer` is not booked for Plan A — the visual grammar of everything touched here already exists.

### Branch topology

Each section on its own branch off `main` (assuming the `fix/token-phase-steal-exhaustion` merge lands first — see cross-plan doc). One PR per branch.

- `fix/gamelog-announcements` — A0 + A1
- `fix/hand-card-playability-contract` — A2
- `fix/info-badge-dismiss` — A4

### Worktree strategy

A2 and A4 both edit `PlayerHand.tsx` — **cannot run in parallel worktrees.** A4 lands first (it's the smaller, InfoBadge-only edit); A2's `PlayerHand` changes rebase on top.

A1 is safe to run in a separate worktree alongside either A2 or A4 — its frontend footprint is a new hook + one `GameBoardPage` insertion, no overlap with `PlayerHand` or `InfoBadge`.

### Conflict-zone map

| File | Sections that touch it |
|---|---|
| `TrashAnimal.Web/src/components/InfoBadge.tsx` | A4 only |
| `TrashAnimal.Web/src/components/gameboard/PlayerHand.tsx` | **A2 + A4 (via InfoBadge usage)** — serialize |
| `TrashAnimal.Web/src/pages/GameBoardPage.tsx` | A1 (new hook call), A2 (drops `shinyDisabledExplanation` computation) — small overlap, resolve manually |
| `TrashAnimal/GameView.cs` | A2 only |
| `TrashAnimal/GameSession.cs` (`GetViewForPlayer`) | A2 only |
| `TrashAnimal/GameLog/GameLogProjector.cs` | A1 only (if option (ii) taken) |
| `TrashAnimal.Web/src/api/types.ts` | A1 + A2 — additive fields, low risk, but coordinate |

### Cross-plan coordination

Plan B's B0 depends on A2's `HandCardView.PlayableAs`. Do **not** start any B section until A2 is merged. See [debug-notes-shared-execution-notes.md](debug-notes-shared-execution-notes.md) for the cross-plan lock order.

---

## A0 — Discovery pass: enumerate all "acted-on player gets no feedback" cases

**Why this exists.** Issue #6 (thief not notified of a Doggo block) is one instance of a class of bug: `GameBoardPage.dispatch()` is the only toast source in the app, and it only fires from mutation `onSuccess` — so **only the acting player is ever toasted.** The recent steal-token bug had the same shape (fix the reported case, discover more instances later); the notes' meta section explicitly flags this pattern. Do the sweep first, then design the mechanism against the full list.

**This is a read-only discovery task. It produces no code — it produces the input for A1's design.**

### Agent

`general-purpose` (needs Grep + Read across both backend and frontend). Do **not** hand this to a specialist reviewer — the task is *"find all cases," not "review one change."*

### Files/directories in scope

Read-only:
- `TrashAnimal/GameLog/GameLogEvent.cs` (event catalog)
- `TrashAnimal/GameLog/GameLogProjector.cs` (per-viewer message rendering)
- `TrashAnimal/GameSession.*.cs` (mutation entry points)
- `TrashAnimal/TokenPhase/**` (token-phase mutations, esp. `TokenPhaseBanditHandler`)
- `TrashAnimal/StealAttempt.cs`
- `TrashAnimal.Web/src/pages/GameBoardPage.tsx` (current toast wiring)
- `TrashAnimal.Web/src/components/Toast/*` (current toast semantics)

### Deliverable

A checklist saved to `.claude/docs/plans/debug-notes-a0-affected-player-cases.md`, one row per case, with columns:
1. **Trigger** — which `GameAction` or `GameLogEvent` causes it
2. **Actor seat** — who's dispatching
3. **Affected seat(s)** — who *should* be notified but isn't
4. **Existing log event** — is there already a projector line for it? (If yes, A1 just consumes it; if no, add one.)
5. **Suggested toast text** — from the *affected* player's viewpoint

### Seed list (non-exhaustive — verify and expand)

- Steal blocked by Doggo (the #6 case) — thief sees no toast
- Kitteh swap — original victim becomes thief; both seats' UX change silently
- Bandit reveals a match on you — you're the affected player, not the actor
- Bandit reveals a match on someone else, you must respond next (queue advance) — you become the actor of the *next* prompt with no announcement
- Steal completes against you — victim sees no toast
- Steal Auto-resolves with no targets (issue #8's case, on the *other* players' side — the acting player gets an info toast, but do the opponents get anything? Probably fine to skip, but confirm.)
- Yum Yum forced re-roll caused by an opponent — the roller sees the reroll happen but no explanation of *who* forced it
- Game ends because you exhausted the deck — every non-actor sees the game end with no attribution

### Sign-off

Return the checklist for user review before A1 starts. A1's mechanism is generic enough to handle N cases, but the *set of cases* determines whether A1's option (ii) (`AffectedPlayerSeat`) needs to be a single seat or a list.

---

## A1 — Issue #6 (and its whole class): notify players who are acted on, not just actors

### Preconditions

A0's checklist must be complete and reviewed. This section's design assumes the full case list is known — do not start coding until A0 is signed off.

### Verified current state

The domain already records and projects the Doggo-block case correctly. `StealBlockedEvent` exists in `TrashAnimal/GameLog/GameLogEvent.cs:71`, and `GameLogProjector.BuildStealBlockedMessage` ([GameLogProjector.cs:87](TrashAnimal/GameLog/GameLogProjector.cs:87)) already renders a viewer-correct message — the thief specifically sees *"Alice played Doggo to block **your** steal."*

**So for #6 specifically, the data is not missing. The problem is that it only ever appears as a passive line in `GameLogPanel`.** A0 will confirm which of the other cases already have a corresponding log event and which need a new one added.

### Approach

Derive toasts from newly-arrived game-log entries, frontend-only. This works because `GameLogEntryView.SequenceNumber` is documented as stable and identical across all viewers (only `Message` differs per viewer), so it is a reliable high-water mark.

Do **not** put this on the SignalR envelope. `GameHub` is push-only by explicit design (see `TrashAnimal.Api/CLAUDE.md`) and must stay a "go re-fetch" trigger; widening it to carry per-player messages would breach that boundary and duplicate the hidden-information redaction `GameLogProjector` already owns.

### Work items

1. **New hook `TrashAnimal.Web/src/hooks/useGameLogAnnouncements.ts`.**
   - Input: `entries: GameLogEntryView[]`, `localSeatIndex: number`.
   - Holds the last-seen `sequenceNumber` in a ref. On each render where new entries appear, select the ones worth announcing and toast them.
   - **Initial mount must seed the high-water mark without toasting** — otherwise a player refreshing mid-game gets the entire backlog fired at them at once.
2. **Selection rule.** Announce an entry when it is *about* the local player but not *by* them: `entry.actingPlayerSeat !== localSeatIndex` **and** the entry names the viewer as affected. Since `Message` is a pre-rendered string, there is no structured "affects seat N" field to key on today.
   - **Decision (deferred to A0):** either (i) substring-match `"your"`/`"you"` in the message — cheap, no backend change, but brittle (same fragile pattern already flagged as a known wart in `JoinForm`'s duplicate-nickname handling); or (ii) **preferred** — add `AffectedPlayerSeats: IReadOnlyList<int>?` (a list, not a single seat — Kitteh-swap and steal-completion each affect two players) to `GameLogEntryView` and populate it in `GameLogProjector`, giving the frontend a real field to filter on.
   - Recommend (ii). It is a small additive change to one record and one projector, and it removes a class of string-matching fragility rather than adding to it. It does mean a backend touch, so it must be reflected in `types.ts` and the contract tests.
   - Whether the field is a single seat or a list depends on A0's case list — Kitteh-swap notifies both old and new victims, so a list is the safe default.
3. **Wire into `GameBoardPage`** next to the existing `useGameSignalR` call, passing `gameView.log` and `localSeatIndex`.
4. **Rate-limit.** A single re-fetch can surface several new entries at once (e.g. block + turn-resolved). Cap at the 2–3 most recent announceable entries per batch so the toast stack cannot flood.

### Tests

- `useGameLogAnnouncements.test.ts` — seeds without toasting on mount; toasts on a genuinely new entry; ignores entries acted by the local seat; respects the batch cap.
- If option (ii) is taken: extend `TrashAnimal.Tests/GameLog/GameLogEmissionTests.cs` for `AffectedPlayerSeat` on `StealBlockedEvent`, `StealRoleSwappedEvent`, `StealCompletedEvent`, and add the field to the frontend contract tests in `src/api/contracts.test.ts`.

---

## A2 — Issue #2: cards that can't be played this turn look playable

### Verified current state

`PlayerHand` ([PlayerHand.tsx](TrashAnimal.Web/src/components/gameboard/PlayerHand.tsx)) has **no per-card playability model whatsoever**. Every card in the fan — regardless of its `CardName` — is wired to the same handler:

```tsx
onClick={() => canPlayFeesh && onFeeshClick()}
```

where `canPlayFeesh = allowedActions.includes('PlayFeesh')`. The whole hand is effectively one "play Feesh" button. `aria-disabled` and `tabIndex` are likewise driven off that single flag, and `cursor: 'pointer'` is applied unconditionally with a comment stating hover is deliberately always live.

The backend cannot help yet either: `HandCardView` is `record HandCardView(Guid CardId, CardName Name)` ([GameView.cs:24](TrashAnimal/GameView.cs:24)) — no playability, no reason, no `NewlyAdded`. The domain **does** track it (`HandEntry.NewlyAdded`, [HandEntry.cs:17](TrashAnimal/HandEntry.cs:17)); it just never crosses the wire.

The note's diagnosis — "expected game behavior, cards can't be played the turn they're drawn" — is correct, but that's only *one* of several reasons a card may be unplayable. The UI needs to express the general case.

### Approach

Push a per-card playability verdict across the contract, then surface it via the existing `InfoBadge`. **This is the foundation Plan B builds on — do it first.**

**Data vs presentation — one source, one surface.** `HandCardView.UnplayableReason` is the *data* (only the domain knows the actual reason — same-turn-drawn vs no-valid-target vs wrong-phase, and copy needs to stay in sync with rule changes). `InfoBadge` is the *presentation* — pop up an on-demand explanation when the player wonders why the card is grayed out. They are not redundant; they are the two halves of the same feature. Do **not** also add a `title=` tooltip — the badge is the single surfacing mechanism, and the two would drift.

### Work items

1. **Domain — extend `HandCardView`** (`TrashAnimal/GameView.cs`):
   ```csharp
   public sealed record HandCardView(
       Guid CardId,
       CardName Name,
       GameAction? PlayableAs,           // null = not playable right now
       string? UnplayableReason);        // human-readable, null when PlayableAs is set
   ```
   `PlayableAs` doubles as the routing key Plan B needs, so the two concerns share one field rather than each inventing their own.
2. **Populate it in `GameSession.GetViewForPlayer`** (`GameSession.cs`).

   **One reason at a time — the most-specific one wins.** Multiple reasons can genuinely apply at once (it isn't your turn *and* the card was drawn this turn); showing all of them stacks noise. Rank reasons from most-specific (about *this card in this moment*) to least-specific (about the game session at large), and return the first that applies.

   **Do not emit the "not your turn" reason at all** (see note below). YumYum is the sole card that already carries its own out-of-turn interrupt affordance; it does not need a per-card badge either.

   Ranking, most-specific first:

   | Rank | Condition | Example `UnplayableReason` |
   |---|---|---|
   | 1 | `entry.NewlyAdded` (drawn this turn) | *"Cards drawn during your current turn cannot be played."* (already user-approved) |
   | 2 | Card's play action doesn't apply in the current phase | *"Blammo! cannot be played during the token resolve phase."*, *"MmmPie cannot be played during the roll phase."* — always name the card and the phase explicitly |
   | 3 | Action exists in the current phase but has no valid target right now | reuse existing rejection text — e.g. *"No opponent has anything in their stash to steal."* from [ShinyPlayHandler.cs:22](TrashAnimal/RollPhase/ShinyPlayHandler.cs:22) |
   | — | It is not the viewer's turn (and no interrupt window is open for this card) | **do not populate a reason — leave `UnplayableReason = null` and `PlayableAs = null`.** The `PlayerHand` grays the card and shows no badge; the top-level `TurnIndicator`/`PhaseToggle` already communicates whose turn it is. |
   | last | Otherwise | `PlayableAs = <action>`, `UnplayableReason = null` |

   **Rank 2 wording contract.** Every rank-2 message must name (a) the exact card (`"Blammo!"`, `"MmmPie"` — use `CardName.ToString()` and let the domain hold the display form) and (b) the phase it *is* legal in, or the phase it *isn't* legal in — pick one direction and be consistent (recommend "cannot be played during the X phase" for negative framing, matches how a player thinks about the moment). Do not fall back to a generic *"Shiny can't be played right now"* — that hides the rule.

   **Reuse the existing eligibility sources — do not re-derive them.** RollPhase verdicts must come from `RollPhaseGameplayHandlerRegistry` / `IGameplayHandler.IsActionable`; TokenPhase verdicts from `TokenPhaseAllowedActionsProvider`. Hand-rolling a parallel rules check here is exactly the "patch the symptom" failure mode called out in the notes' meta section, and it will drift.

   **Why "not your turn" is deliberately omitted:** the state is already visible globally (`TurnIndicator`, `PhaseToggle`, and the whole board's actor coloring), so per-card badges saying it would be redundant clutter — potentially 5–7 identical badges when the player just wants to look at their hand. Interrupt windows (Yum Yum, Bandit response, Steal response) already open their own dedicated UI, so an out-of-turn player who *is* allowed to act right now isn't relying on hand-card badges either. If a future card ever gains an out-of-turn interrupt without a dedicated modal, revisit this — but that's a real design decision, not something to preempt with defensive UI now.
3. **Frontend contract** — mirror the two new fields on `HandCardView` in `src/api/types.ts`; extend `src/api/contracts.test.ts`.
4. **`PlayerHand` rendering:**
   - unplayable → `opacity: 0.55` + `filter: grayscale(0.6)` on the card image (generalize the existing Shiny-specific `opacity: 0.5` at [PlayerHand.tsx:42](TrashAnimal.Web/src/components/gameboard/PlayerHand.tsx:42) — that special case goes away)
   - unplayable → `cursor: 'not-allowed'`, `aria-disabled={true}`, `tabIndex={-1}` — **no `title=` attribute** (the badge owns the explanation)
   - **keep hover-to-fan alive for every card regardless of playability.** The comment at [PlayerHand.tsx:49](TrashAnimal.Web/src/components/gameboard/PlayerHand.tsx:49) documents a real bug a previous pass already hit and fixed; do not regress it.
   - render the `InfoBadge` for **any** card with an `unplayableReason`, not just Shiny — depends on A4 landing so the badge is dismissible. Playable cards render no badge (the current Shiny-only special-case is replaced by "badge iff unplayable reason").

### Tests

- `TrashAnimal.Tests` — a `GameSessionHandCardPlayabilityTests` class:
  - a newly-drawn card reports the drawn-this-turn reason; the same card reports `PlayableAs` on the following turn
  - **ranking:** a card that is both newly-drawn *and* wrong-phase reports only the newly-drawn (rank 1) reason
  - a wrong-phase card (e.g. MmmPie during RollPhase) reports the phase-specific message naming both the card and the phase
  - Shiny with all opponent stashes empty reports the target-unavailable (rank 3) reason
  - a non-active player with no open interrupt window sees every card with `PlayableAs = null` **and `UnplayableReason = null`**
- `PlayerHand.test.tsx` — unplayable cards are `aria-disabled` and don't fire the click handler; playable ones do; **hovering an unplayable card still fans the hand;** a card with `unplayableReason = null` (out-of-turn case) renders grayed-out with no `InfoBadge`.

---

## A4 — Issue #16: info badge can't be dismissed

### Verified current state — the note's stated root cause is wrong

The note attributes this to *"the disabled state is blocking click events on the info button."* That isn't what's happening. `InfoBadge` ([InfoBadge.tsx](TrashAnimal.Web/src/components/InfoBadge.tsx)) already calls `event.stopPropagation()` and already toggles `isPinned` correctly, and the parent in `PlayerHand` is a plain `<div role="button">`, not a native `disabled` control — nothing is swallowing the click.

The actual cause is the visibility expression at [InfoBadge.tsx:23](TrashAnimal.Web/src/components/InfoBadge.tsx:23):

```tsx
const isVisible = Boolean(info) && (isHovering || isFocused || isPinned);
```

`isHovering` is set by `onMouseEnter` on the **wrapper div**, which contains both the card and the badge. So while the pointer is anywhere over the card, `isHovering` is `true` and the bubble is visible **no matter what `isPinned` is**. Clicking the badge flips `isPinned` true→false exactly as designed, and the bubble does not move — indistinguishable from "the button doesn't work." On touch, most mobile browsers synthesize `mouseenter` on tap, so it sticks there too.

The Shiny card is where it was noticed only because Shiny is the sole card currently receiving a non-null `info` (`shinyDisabledExplanation`, [GameBoardPage.tsx:138](TrashAnimal.Web/src/pages/GameBoardPage.tsx:138)). After A2 this affects every unplayable card, so fix it before A2's badge rollout.

### Approach

Pinning is a three-state concept, not a boolean OR'd with hover. An explicit dismissal must be able to beat hover.

### Work items

1. Replace the `isPinned` boolean with `pinState: 'unset' | 'pinned' | 'dismissed'`.
2. Recompute visibility:
   ```tsx
   const isVisible = Boolean(info) && pinState !== 'dismissed' && (isHovering || isFocused || pinState === 'pinned');
   ```
3. Badge click cycles: currently-visible → `'dismissed'`; currently-hidden → `'pinned'`.
4. Reset `pinState` to `'unset'` on `mouseleave`, so a later hover behaves normally instead of staying permanently suppressed.
5. The existing outside-click and `Escape` listeners should also reset to `'unset'` (not `'dismissed'`) — they mean "stop showing this now", not "never show it again."
6. Keep the `stopPropagation` — it is correct, and becomes load-bearing once Plan B makes the card itself clickable.

### Tests

Extend `InfoBadge.test.tsx`:
- bubble appears on hover; **clicking the badge while hovering hides it and it stays hidden while the pointer remains over the card** (the actual reported bug — assert it directly)
- mouse-leave then re-hover shows it again
- click while not hovering pins it open; it survives a subsequent mouse-leave
- Escape and outside-click both close it, and a later hover still works

---

## Suggested execution order

1. **A0** (discovery, no code — produces the A1 case list; ~1 agent-hour)
2. **A4** (self-contained, no contract change, unblocks A2's badge usage)
3. **A2** (contract change — Plan B depends on it; land it early)
4. **A0 sign-off**, then **A1** (contract change if option (ii) is chosen)

**Parallelism:** A0 and A4 can run concurrently (A0 is read-only, A4 doesn't touch anything A0 reads). A2 must wait for A4 to merge (both touch `PlayerHand.tsx`). A1 waits for A0's checklist.

## Open questions for the user

1. **A1 selection rule** — no answer needed yet; resolved at A0 sign-off (recommendation stands: `AffectedPlayerSeats: int[]?` on `GameLogEntryView`).

(A2 reason wording — resolved: rank-ordered reasons, Blammo!/MmmPie-style phase-naming copy, no "not your turn" badge.)
