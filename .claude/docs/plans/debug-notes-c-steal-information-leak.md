# Plan C — Steal-Target Information Leak

**Source:** `DEBUG_NOTES.md` issue #9
(notes file lives outside the repo, in the Claude scratchpad — not tracked in git)
**Scope:** the steal-pick UI leaks *which* card the victim just acquired. Fix by (i) not sending ordering information the thief shouldn't know, and (ii) rendering the face-down slots as an unordered fan of card backs rather than an ordered list.
**Sibling plans:** [Plan A — correctness & blocked states](debug-notes-a-correctness-and-blocked-states.md), [Plan B — card-selection interaction model](debug-notes-b-card-selection-interaction-model.md). Plan C has no ordering dependency on A or B's *content* — but see the blocker below, which applies to Plan C too.

> **⛔ Hard blocker: `fix/token-phase-steal-exhaustion` must land on `main` before any section here starts.** `TrashAnimal/GameSession.cs`, `GameSession.ApiSupport.cs`, and the `TokenPhase/**` tree are all dirty/uncommitted on that branch, and `StealAttempt.cs` (which C3 modifies) is a likely concurrent-edit collision with that branch's steal-exhaustion fix. This plan's independence from A/B is about *sequencing relative to them*, not an exemption from this blocker. See [debug-notes-shared-execution-notes.md](debug-notes-shared-execution-notes.md) for the full evidence.

---

## Execution Model

### Agent assignments

| Section | Owner agent | Notes |
|---|---|---|
| **C1 Shuffle projection** | `backend-engineer` | Pure domain work; add tests as you go. |
| **C2 Sweep** | `general-purpose` (read-only sweep + short report), then `backend-engineer` for any fixes | Report first, decide-then-implement. |
| **C3 Stability across re-fetches** | `backend-engineer` | Depends on C1's builder signature. |
| **C4 Face-down fan UI** | `ui-designer` for `StealPickFan.tsx` (new visual component), then `frontend-engineer` to swap into `StealPrompt` | The fan geometry is a real design call. |

`security-reviewer` should be booked to look at the final PR (this is a fairness/hidden-info issue — the class the reviewer explicitly covers).

### Branch topology

Single branch `fix/steal-target-info-leak` for all four sections. They're tightly coupled — the shuffle in C1 is meaningless without C3's stability, and C4 without either is UI theater over a leaky server. Ship as one PR.

### Worktree strategy

**Serial within the branch.** C1 → C3 → C2 → C4 (that order matters — C3 needs C1's builder shape, C4's `StealPickFan` needs C1+C3's backend contract to be stable). Do **not** try to worktree these apart; the shared state on `StealAttempt` (C3) and the shared `StealPickSlot` shape make parallelism actively risky.

C2's sweep is the one thing that can genuinely run in parallel — it's read-only. Kick off the sweep in a `general-purpose` agent at the same time as C1's implementation; consume its report before starting C3.

### Conflict-zone map

Inside the plan, no meaningful conflicts (serial). Externally:

| File | Also touched by |
|---|---|
| `TrashAnimal/StealPickSlotBuilder.cs` | Plan C only |
| `TrashAnimal/StealPhaseView.cs` | Plan C only |
| `TrashAnimal/StealAttempt.cs` | Plan C (C3) — check `fix/token-phase-steal-exhaustion` for concurrent changes before starting |
| `TrashAnimal.Web/src/components/gameboard/StealPrompt.tsx` | Plan C only |
| `TrashAnimal.Web/src/api/types.ts` | Plans A + B + C — additive fields, low risk, but coordinate merges |

### Cross-plan coordination

C is independent of A and B. It can start immediately; only the shared `types.ts` file needs merge attention.

See [debug-notes-shared-execution-notes.md](debug-notes-shared-execution-notes.md) for the cross-plan lock order.

---

## Framing

Every other issue in the notes is a UI defect. This one is a **rules-integrity defect**: the game surface is telling the thief something the game rules say they shouldn't know. UI-only shuffling isn't enough; the leak has to be closed at the *view projection* level, or a determined player can just open devtools and read the network response.

---

## Verified current state

### The leak is real and lives at two layers

**Domain (source of the leak).** `StealPickSlotBuilder.BuildForThief` ([StealPickSlotBuilder.cs:5](TrashAnimal/StealPickSlotBuilder.cs:5)):

```csharp
return victim.Hand
    .Select(e => new StealPickSlot(e.Card.Id, StealPickSlot.UnrevealedLabel))
    .ToList();
```

The slots are returned **in `victim.Hand` iteration order** — i.e. draw/acquisition order, since `Hand.Add` appends. Every slot label is the constant `"Unrevealed Card"`, so the labels themselves reveal nothing — but the *position* in the list does. If the victim just drew via Feesh or MmmPie, the tail slot is the newly-drawn card. The note's observation is accurate.

Same story for stash steals when any stashed card is face-down: `victim.StashPile` iteration order is stash-time order.

**Wire.** `StealPickSlot` carries `(Guid CardId, string ThiefFacingLabel)`; the C# `Guid` values themselves are v4 (random), so they don't encode ordering — but the *array order* in JSON is preserved end-to-end and directly consumed by the thief's UI.

**Frontend.** `StealPrompt` ([StealPrompt.tsx:78](TrashAnimal.Web/src/components/gameboard/StealPrompt.tsx:78)) renders `stealPhase.thiefPickSlots.map(...)` as a flex-wrap of text buttons — exact server order, no shuffle. `VictimPicker` ([VictimPicker.tsx:20](TrashAnimal.Web/src/components/gameboard/VictimPicker.tsx:20)) also relies on `handCount` in the opponent summary, but that's a count only — no ordering leak.

### What the thief is genuinely allowed to know

Just the set of `CardId`s they may pick — nothing more. Not order. Not "how many are face-down vs face-up in a stash steal" beyond what's necessary to render the face-up ones' names. The rules already permit revealing face-up stash card names ([StealPickSlotBuilder.cs:11](TrashAnimal/StealPickSlotBuilder.cs:11)) — that's a deliberate face-up-card affordance, not a leak.

---

## C1 — Backend: strip ordering from the projection

### Approach

Randomize the slot order **at the projection layer** — inside `StealPickSlotBuilder`, before the list is handed to `StealPhaseView`. The projection is per-request, so shuffle-per-projection is fine: the thief picks by `CardId`, not by index, so the shuffle can even re-run across polls without breaking any resolution logic (see C3 for a caveat on that).

### Work items

1. **Introduce a seeded shuffle.** `StealPickSlotBuilder.BuildForThief` becomes:
   ```csharp
   public static IReadOnlyList<StealPickSlot> BuildForThief(
       StealTargetZone zone, Player victim, Random shuffle) { ... }
   ```
   Callers pass in an RNG. All slot construction stays the same; shuffle the resulting list with Fisher–Yates using the supplied `Random` before returning it.
2. **RNG source.** Do **not** call `new Random()` inside the builder — that makes the pipeline non-deterministic and untestable. Two options:
   - **(a)** Thread the existing `Random`/`Die` seam through. `GameSession` already accepts a `Die` in its constructor (`Die.Roll()` is virtual — same seam the `Mock<Die>` factory uses in tests). Add a companion `Random Shuffle` (or expose `Die`'s underlying `Random`) and thread it into the builder call site.
   - **(b)** Add a new `IShuffler` seam (`Shuffle<T>(IList<T> items)`) with a default implementation over `Random`. More explicit; more DI plumbing.
   - **Recommended (a)** — reuses an established seam and keeps the "one RNG per session" model. If (a) turns out to require exposing something on `Die` that shouldn't be public, fall back to (b).
3. **Repeat for the stash steal branch.** The stash-zone branch also returns slots in `victim.StashPile` order. Shuffle it the same way. Face-up cards still carry their revealed name in `ThiefFacingLabel` — shuffling doesn't leak them; it only breaks the *"the last face-down slot is the most recently stashed"* inference.
4. **Do not change `StealPickSlot`'s shape.** `CardId` stays load-bearing for resolution; the shuffle is purely on list order.

### Tests

- `TrashAnimal.Tests/StealPickSlotBuilderTests.cs` — new tests:
  - Hand-zone steal with a deterministic `Random` seed produces a slot order that is not `victim.Hand`'s order (verify via a hand where original order is known and the seed produces a specific permutation).
  - Every `CardId` in `victim.Hand` appears exactly once in the output.
  - Stash-zone steal shuffles too, and face-up labels still resolve to the correct names.
  - `ThiefFacingLabel` remains `"Unrevealed Card"` for every hand slot.

---

## C2 — Scope decision: shuffling is limited to steal-target zones only

**Resolved.** Shuffling only matters when the thief is picking a card to steal from an opponent's **hand or face-down stash** — that's the only place a positional cue leaks something the thief isn't supposed to know (which card was most recently acquired). Every other view in the sweep list stays untouched:

- ✅ `StealPickSlotBuilder` (hand-zone and stash-zone steal targets) — **the only in-scope case.** Covered by C1.
- ❌ `OpponentSummaryView.StashFaceUpCards` — face-up cards are already fully public (name and existence both known); no shuffle.
- ❌ `VictimPicker` seat list — public seat order; no shuffle.
- ❌ Bandit reveal — single card, no ordering to leak.
- ❌ `DiscardPile` / `FeeshCardPicker` — public information end to end; no shuffle.

No further sweep work items. C2 is now purely documentation: add a one-line comment on `StealPickSlotBuilder` stating that shuffling is deliberately scoped to steal-pick projections and is not a general "shuffle hidden collections" policy, so a future contributor doesn't assume the pattern should propagate elsewhere without re-checking whether a real leak exists there.

### Why the shuffle is safe (confirmed against the code)

`StealPickSlotBuilder.BuildForThief` builds a **fresh, disposable projection list** from `victim.Hand` / `victim.StashPile` on every call — it never mutates those underlying collections, only returns a new `List<StealPickSlot>` derived from them. The victim's own hand view (`HandCardView`, built separately and directly from `victim.Hand` in `GetViewForPlayer`) is completely untouched by this shuffle — the victim always sees their own cards in the game's real order. Card resolution is always keyed by `CardId` (a stable `Guid`), never by list position, so there is no path by which shuffling display order could cause an ID to resolve to the wrong card. The shuffle is purely a presentation-order concern, scoped to what the *thief* sees for the duration of one pick, and has no persistence or identity implications.

---

## C3 — Backend: view stability across re-fetches

**Resolved: go with per-attempt cached order.** This is directly downstream of C1's shuffle, not a separate concern — it's the thing that keeps C1's fix from creating a new problem.

### Why this exists

`GameHub` push → client re-fetch → new `GetViewForPlayer` call → **fresh shuffle** (as C1 implements it). That push isn't limited to the thief's own actions — it fires on *any* player's revision-bumping move. So while the thief is still staring at the pick prompt deciding, an unrelated action elsewhere in the game triggers their client to re-fetch, and — with no stabilization — the slots would visibly rearrange mid-decision. Confusing at best; at worst it reintroduces a version of the same leak, since comparing two different shuffles of the same set can itself carry information.

Cache the shuffled order per steal attempt.

### Work items

1. Add `IReadOnlyList<Guid>? ThiefPickOrder` (or similar) to `StealAttempt`.
2. Populate it once when the thief-pick prompt is first constructed (`StealAttempt.Begin` or wherever the pick-slot list is first materialized).
3. Change `StealPickSlotBuilder.BuildForThief` to accept an optional `IReadOnlyList<Guid>? preferredOrder`; when supplied, arrange the resulting slots to match that order (fall back to shuffling for slots whose `CardId` isn't in `preferredOrder`, though in practice all should be).
4. Clear `ThiefPickOrder` on `StealAttempt.End`/aftermath.

### Tests

- Two back-to-back `GetViewForPlayer` calls for the thief during a pending steal return `ThiefPickSlots` in the same order.
- A subsequent, different steal attempt shuffles independently.

---

## C4 — Frontend: render slots as an unordered face-down fan, not a text list

### Verified current state

`StealPrompt` ([StealPrompt.tsx:71–92](TrashAnimal.Web/src/components/gameboard/StealPrompt.tsx:71)) renders each slot as a gb-glass rectangular button showing `slot.thiefFacingLabel` as text (`"Unrevealed Card"` for hand steals; card names for revealed stash cards). The note calls for a hand-style layout with face-down card-back images.

### Approach

**Resolved: flat, evenly-spaced spread — not `PlayerHand`'s bespoke fan/hover geometry.** Simpler layout, no per-card hover-lift, no scaling. Card-back asset for unrevealed slots, real card art for revealed (face-up stash) slots.

**Focus requirement: nothing behind the picker should compete for attention.** Verified this is already structurally satisfied — `StealPrompt` already renders every branch inside the shared `<Modal>` shell ([Modal.tsx](TrashAnimal.Web/src/components/gameboard/Modal.tsx)), which provides a full-screen scrim (`rgba(5,10,20,.68)`) **and `backdrop-filter: blur(3px)`** over everything behind it — the player's own hand, token tray, deck/discard piles, all blurred and dimmed the instant the modal mounts. `StealPickFan` doesn't need to invent this; it just needs to not undermine it (no transparent backgrounds on the fan's own container that would let background motion/color bleed through, no z-index tricks that escape the modal).

### Work items

1. **New component `TrashAnimal.Web/src/components/gameboard/StealPickFan.tsx`.**
   - Props:
     ```ts
     interface StealPickFanProps {
       slots: StealPickSlot[];
       onPick: (cardId: string) => void;
       isPending: boolean;
     }
     ```
   - Renders each slot as a card-sized tile. Rule for what's on the tile:
     - `slot.thiefFacingLabel === StealPickSlot.UnrevealedLabel` (from the backend constant — mirror this into `types.ts` as `STEAL_PICK_SLOT_UNREVEALED_LABEL` so the frontend has a single source of truth) → `CARD_BACK_IMAGE` with `alt="Unrevealed card"`.
     - otherwise → `CARD_IMAGE_BY_NAME[slot.thiefFacingLabel as CardName]` (guarded — fallback to card-back if the label isn't a known name), with `alt={slot.thiefFacingLabel}`.
   - Layout: flat, evenly-spaced horizontal row — flex-wrap is fine, matching the sizing already used for other card-pick grids (`StashModal`/`GroupedCardPicker`-scale tiles, not full `PlayerHand`-scale). No hover-lift, no per-tile scale, no rotation. Every tile is a `<button>` firing `onPick(slot.cardId)`.
   - Fixed tile size regardless of slot count. That, plus C1's shuffle, plus C3's stability, makes card position uninformative even before considering positional cues.
2. **Replace the text-button block** in `StealPrompt.tsx` at [StealPrompt.tsx:77–89](TrashAnimal.Web/src/components/gameboard/StealPrompt.tsx:77) with `<StealPickFan slots={stealPhase.thiefPickSlots} onPick={onCardPick} isPending={isPending} />`, still inside the existing `<Modal>` wrapper — do not change or bypass the modal shell, since it's already delivering the requested focus/dimming behavior.
3. **Do not add any client-side shuffle.** The backend (C1+C3) is the source of truth; a UI shuffle would just add a second independent RNG and re-open the "re-render → new order" leak that C3 closes.

### Tests

- `StealPickFan.test.tsx` — unrevealed slots render the card-back `alt="Unrevealed card"`; face-up (revealed) slots render the corresponding card image; clicking a tile calls `onPick` with that tile's `cardId`; every tile is disabled while `isPending`.
- `StealPrompt.test.tsx` — update the assertion that used to look for the old text-button labels; verify the fan is rendered when `state === 'AwaitingStealCardPick'` and `isThief`.

---

## Verification checklist (once C1–C4 land)

The notes' meta section explicitly asks for live verification, not just tests. Do all of these in a real browser session:

1. Play Feesh, retrieve a specific known card into hand, wait for a Steal token to come at you from an opponent — inspect the thief's UI (viewed as the other player) and confirm the just-retrieved card cannot be spotted by position.
2. Same again for a stash steal: face-down-stash a card via MmmPie/Steal-then-stashtrash and confirm its position in the thief's view is unrelated to when it was stashed.
3. Force a re-render on the thief side during the pick prompt (e.g. tab away and back, or trigger a benign action from a third player) — confirm the pick-slot order **does not** change between renders while the same pick is pending (C3 acceptance).
4. Peek at the JSON response from `POST /games/{id}/commands` (or the subsequent `GET /games/{id}/view`) — confirm the `thiefPickSlots` array's `cardId` order does not correspond to any obvious source-of-truth order.

## Open questions for the user

None outstanding. (C2: shuffling scoped to steal-target hand/stash zones only, everything else documented as public. C3: per-attempt cached shuffle order. C4: flat evenly-spaced spread, relying on the existing `Modal` scrim/blur for focus — no new dimming mechanism needed.)
