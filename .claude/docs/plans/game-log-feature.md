# Game Log Feature

## Context

The game board UI (screenshot reference) shows a scrollable "GAME LOG" panel listing a running history of what happened during the game — one line per notable action, across all players, newest at bottom, tagged with a turn number ("You stashed 3 Feesh. Turn 1", "Rita Raccoon drew a Blammo! Turn 2", "Bandit BJ rolled Steal on you. Turn 2", "You played Doggo to block a steal. Turn 2").

Nothing like this exists today. Exploration confirmed there is **no event/audit trail anywhere** in the system — `GameSession` is a pure state machine that only exposes current-state snapshots via `GetViewForPlayer()`; the API only tracks a monotonic `Revision` counter; SignalR's `GameUpdateEnvelope` is trigger-only ("something changed, go refetch"). Adding a game log is therefore a genuine new capability, not wiring up something that's just not surfaced yet.

Two goals were discussed:
1. **Now**: let players read a history of what's happened in the current game (the screenshot's UX).
2. **Later (not this pass)**: potentially use that history to persist/resume a game that was exited before it ended.

Decisions made with the user to shape this without painting into a corner:
- **Log entries originate on the backend as structured domain events**, not client-inferred strings and not a thin "summary string per command" hybrid — this is what makes goal 2 later additive instead of a rewrite.
- **The log covers all players in real time**, matching the screenshot — not just the local player's own actions.
- **No persistence/resume is built now.** The event log lives in memory on `GameSession`, exactly like every other piece of session state today (lost on API restart) — that's an accepted trade-off, not a gap to fix in this pass.

### Pattern name (for future reference)

The design below is a recognized combination of two patterns, not an ad hoc scheme:

- **Domain Events** (Domain-Driven Design): a meaningful thing that happened is recorded as an immutable, past-tense, structured object (`CardStashedEvent`, `PlayerBustedEvent`, etc.) rather than a mutable log row or a display string. This is the core building block.
- **Projection** (a CQRS idea): the same event stream is transformed into different read-facing views depending on who's asking — `GameLogProjector.BuildForViewer` turns one event into a different `Message` per viewer, respecting hidden-information rules.

This is **explicitly not full Event Sourcing**. In true event sourcing, the events *are* the source of truth and current state is derived by replaying them. Here, `GameSession`'s existing mutable fields (`Hand`, `StashPile`, `PhaseOneState`, etc.) remain the actual source of truth exactly as today — the event list is a parallel, append-only record kept alongside that state, not derived from it and not replacing it. That's why this feature can be added without touching how `GameSession` computes state: it's strictly additive.

**Future direction flagged by the user:** there's interest in eventually refactoring `GameSession` toward genuine event sourcing — i.e. making the event stream the actual source of truth, with current state (`Hand`, `StashPile`, etc.) becoming a derived/rebuildable projection of the events rather than independently mutated fields. This game-log feature is a deliberate, compatible first step toward that (the event shape — `SequenceNumber`, structured IDs/enums, no display strings baked in — was chosen with that possibility in mind), but does not attempt it now: `GameSession` keeps mutating its own fields directly, and the event list is descriptive/parallel, not authoritative. A future event-sourcing refactor would be a separate, larger effort (rebuilding `GameSession` state from `GameLogEvent` replay, snapshotting for performance, etc.) and is out of scope here.

**Composite events — a design note for that future refactor.** While reviewing an earlier draft event (`TurnStoppedAndBankedEvent`, since split — see the event-timing note below), a broader question came up: can/should this pattern support a single event representing multiple facts or side effects at once? The answer differs slightly by regime:

- **In this Domain Events + Projection design**, the rule of thumb is *one event = one fact, true at one instant*. Merging facts that become true at different times (e.g. "stopped rolling" at T1, "banked N cards" at T2 after TokenPhase resolves) is an anti-pattern — it forces either an early event with incomplete/guessed data, or a late event that silently drops the earlier fact. That's why the stop is modeled as three separate events (`TurnStoppedRollingEvent`, `YumYumForcedRerollEvent`, `TurnResolvedEvent`) rather than one. A composite event is only appropriate for facts that are genuinely simultaneous — all true at once, as one atomic outcome of a single command. Even then, the preferred approach is usually several small linked events (sharing a `CorrelationId`/`CausationId` so consumers can regroup them) rather than one coarse event, since coarse events block future consumers that only care about one sub-fact. A single coarse event is a legitimate but deliberate granularity trade-off, not a default.
- **In full Event Sourcing**, the same rule holds more strictly, because the stream is replayed to rebuild state, not just read as a log — an unrecorded intermediate fact can never be recovered later. The standard tool for a workflow spanning multiple events over time (stop → Yum Yum window → maybe reroll → TokenPhase resolution is a textbook case) is a **Process Manager / Saga**: a component that reacts to the event stream and issues the next command, rather than one event trying to represent the whole workflow. Worth noting: `TokenPhaseCoordinator` is already playing this role today, just state-based rather than event-driven — it's the natural seam a future ES migration would build on.

### Project rule: event granularity

Adopt as a standing rule for `GameLogEvent` (and any future domain event added to this project): **one event = one fact, true at one instant.** Do not merge facts that become true at different points in time into a single event just because they're part of the same player action or turn (this is exactly the mistake the original `TurnStoppedAndBankedEvent` draft made, before being split into `TurnStoppedRollingEvent` / `YumYumForcedRerollEvent` / `TurnResolvedEvent`).

This rule does **not** ban composite events outright — a single event covering multiple simultaneous sub-facts is still allowed when those facts are genuinely true at the same instant, as one atomic outcome of one command, and are never independently meaningful to a consumer. When that's the case, prefer several small linked events (sharing a correlation/causation identifier) over one coarse event, unless the sub-facts truly have no independent value.

Action item during implementation: fold this rule into `TrashAnimal/CLAUDE.md` (under a "Domain Events" section, alongside the existing "Key Types" documentation) so it's discoverable as a standing project convention, not just a note in this plan.

### Known gaps for a future resume feature

This plan deliberately does not build resume/persistence (see scope boundary below). If/when that's picked up, expect these specific issues — flagged now so they aren't a surprise later:

1. ~~**Events don't carry enough to reconstruct state.**~~ **Resolved in this pass.** Raw events (`CardStashedEvent`, `CardDrawnPrivatelyEvent`, `StealCompletedEvent`) now carry actual `CardId`/`CardName` identity (see §1 "Card identity is captured raw"), redacted only at projection time, not at storage. A future consumer with appropriate access can reconstruct exact `Hand`/`StashPile` contents from the raw event stream. This also substantially closes gap 2 below for card draws specifically, since every card leaving the deck is logged with identity at the moment it's drawn.
2. **Non-determinism beyond card identity.** Card draws are now covered (see gap 1), but die rolls for token faces are still just recorded as their outcome (`TokenDrawnEvent.Token`) — that's already sufficient to replay deterministically (the outcome, not the seed, is what's recorded) — so this gap is narrower than originally scoped: mainly a matter of confirming every RNG-driven outcome that matters for replay (roll results, draw order) has a corresponding event, not a redaction conflict anymore. *(Separately discussed, not yet resolved: an alternative "command sourcing" approach — capturing the deck's shuffled order at game creation plus the submitted-command sequence, to allow deterministic replay by re-executing commands through a replay deck. Trade-off vs. the per-draw identity approach above: command replay is fragile to future rule changes since it re-runs old commands through possibly-new logic, whereas recorded facts don't have that problem; command replay is cheaper to build now but more expensive to replay for long games. The two approaches compose and are not mutually exclusive. Revisit and decide before a resume feature is built.)*
3. **No durability today, regardless of event vs. state.** The log lives in memory on `GameSession` exactly like everything else — an API restart loses both. Event-sourcing the log doesn't solve "resume after restart" by itself; that needs a real durable store keyed by `GameId`, a separate piece of work whether storing events or plain state snapshots.
4. **Pending/in-progress state isn't itself an event.** Resuming mid-`TokenPhase`, mid-`AwaitingStealResponse`, or mid-Yum-Yum-window needs to reconstruct *whose decision is pending and what the options are* — today that's live-inferred from `GameSession`'s fields, not a discrete completed fact in the log. Would need either a pending-state snapshot alongside the log, or events expressive enough to infer it reliably (fragile today — e.g. "awaiting steal response" would have to be inferred from a `StealAttemptedEvent` with no matching resolution event yet).
5. **Client identity/rejoin is a separate problem.** Reconnecting a specific browser session to the correct player seat (`useGameClientIdentity`) is its own flow, unrelated to the event log.
6. **Schema evolution, once persisted.** Event shapes can change freely today (e.g. the stop-event split earlier in this plan). Once durably stored, changing a shape becomes a breaking change for already-saved games — needs a versioning/migration story that doesn't exist yet.
7. **Redaction has to survive persistence.** Today redaction happens per-request at read time from the live in-memory list. A persisted log used for recovery (per the backlog note on a future recovery endpoint) has to keep enforcing the same per-viewer rules on every future read, not just the first one — easy to accidentally weaken later if the persisted form stores pre-redacted or over-broad data. This gets slightly more important now that raw events carry real card identity (gap 1) — the redaction boundary is doing more work than before, since there's more sensitive data sitting in the raw stream to accidentally leak.

## Branching

Before any implementation work starts, create a new branch from `main` (not off the current `gameLog` branch, and not committed directly to `main`) — e.g. `feature/game-log`. All work described below happens on that branch.

## Architecture

### 1. Domain event model — new `TrashAnimal/GameLog/` folder

`GameLogEvent.cs` — a closed hierarchy (matches the existing `GameCommandRequest` discriminated-union pattern), one record type per meaningful mutation:

```csharp
public abstract record GameLogEvent(int SequenceNumber, int TurnNumber, int ActingPlayerSeat);

public sealed record CardStashedEvent(..., IReadOnlyList<Guid> CardIds, IReadOnlyList<CardName> CardNames, bool WasFaceUp) : GameLogEvent(...);
public sealed record TokenDrawnEvent(..., TokenAction Token) : GameLogEvent(...);
public sealed record CardDrawnFaceUpEvent(..., CardName Card) : GameLogEvent(...);      // Bandit reveal
public sealed record CardDrawnPrivatelyEvent(..., IReadOnlyList<Guid> CardIds, IReadOnlyList<CardName> CardNames) : GameLogEvent(...); // hidden draws — identity stored raw, redacted at projection
public sealed record StealAttemptedEvent(..., int TargetSeat, StealTargetZone Zone, CardName? SourceCard) : GameLogEvent(...);
public sealed record StealBlockedEvent(..., int ThiefSeat, CardName BlockingCard) : GameLogEvent(...);
public sealed record StealRoleSwappedEvent(..., int NewVictimSeat) : GameLogEvent(...);  // Kitteh
public sealed record StealCompletedEvent(..., int VictimSeat, StealTargetZone Zone, Guid CardId, CardName CardName) : GameLogEvent(...);
public sealed record CardPlayedEvent(..., CardName Card, int? TargetSeat) : GameLogEvent(...);
public sealed record PlayerBustedEvent(...) : GameLogEvent(...);
public sealed record TurnStoppedRollingEvent(...) : GameLogEvent(...);           // requested stop, entering Yum Yum window
public sealed record YumYumForcedRerollEvent(..., int OpponentSeat) : GameLogEvent(...); // opponent blocked the stop
public sealed record TurnResolvedEvent(...) : GameLogEvent(...); // stop confirmed, TokenPhase finished — no count carried, individual CardStashedEvent/etc. already reported that as it happened
public sealed record TurnEndedEvent(...) : GameLogEvent(...);
public sealed record GameEndedEvent(..., int WinningPlayerSeat) : GameLogEvent(...);
```

Every event stores only IDs/enums/counts — never full hand/stash snapshots, never a pre-rendered display string. `SequenceNumber` (assigned at emission, monotonically increasing) and `TurnNumber` are what make a future durable-store/replay pass additive: persist `(GameId, SequenceNumber) → GameLogEvent` later without touching this shape. Add a `TurnNumber` counter to `GameSession` (increment in `BeginTurn`) — it doesn't exist today.

**Card identity is captured raw, redacted only at projection (in scope for this pass).** `CardStashedEvent`, `CardDrawnPrivatelyEvent`, and `StealCompletedEvent` carry the actual `CardId`/`CardName` of the card(s) involved, even though that identity is hidden information for most viewers. This is safe because redaction happens downstream in `GameLogProjector` (§3), never at storage — the raw event is never sent to the client as-is, only the projected `GameLogEntryView.Message` is. The cost is small: every emission call site already has the actual `Card`/`CardId` in local scope at the moment it emits (it's not new data to compute, just data that would otherwise be dropped), so this is a same-file, additive change rather than new plumbing. The payoff: it closes the biggest gap toward a future replay/resume feature (see §"Known gaps," gap 1) — a future consumer with appropriate access could reconstruct exact `Hand`/`StashPile` contents from the raw event stream, and since every card leaving the deck is now logged with identity at the moment it's drawn, replay wouldn't need to re-simulate the shuffle either.

`IGameLogRecorder` / `GameLogRecorder` — `internal` (matches `TokenPhaseCoordinator`'s visibility), owned 1:1 by `GameSession` like `_steal`/`_tokenPhaseCoordinator`/`_yumYumWindow`. Plain in-memory `List<GameLogEvent>`, unbounded (no pruning/pagination — call this a deliberate deferred trade-off, games are small enough that this is a non-issue today).

### 2. Emission — hook at `GameSession`'s existing boundary methods, not inside RollPhase handlers

Four RollPhase handlers (`ShinyPlayHandler`, `FeeshPlayHandler`, `NannersBustRecoveryHandler`, `BlammoBustRecoveryHandler`) all funnel through **one** call site: `TryExecuteRollPhaseHandler` in `GameSession.StealYumRoll.cs`. Hook there once, using a small `GameAction → GameLogEvent` mapping helper (`TrashAnimal/GameLog/RollPhaseLogEventFactory.cs`, sibling to the existing `TokenPhaseGameActionMapping.cs` pattern) rather than a switch inline. Reuse the same factory from `GameSession.ApiSupport.cs`'s three explicit-choice methods (`TryPlayFeeshWithCardChoice`, `TryPlayShinyWithVictimChoice`, `TryStartTokenStealWithVictimChoice`), which duplicate the handler logic for the API's HTTP path — using one shared factory keeps the two paths' log wording from drifting apart.

Other hook points (all files that already exist and already sit at the right boundary — no new plumbing needed into `RollPhasePlayContext` or `StealAttempt`, which stay session-agnostic):
- `GameSession.StealYumRoll.cs`: `TryStealPlayDoggo` → `StealBlockedEvent`; `TryStealPlayKitteh` → `StealRoleSwappedEvent`; `TryCompleteStealWithCard` → `StealCompletedEvent`; `TryRequestVoluntaryStop` → `TurnStoppedRollingEvent` (fires immediately on the stop request, entering the Yum Yum window — see event-timing note below); `TryYumYumRespond` (the `onYumYumPlayedAllowRollsAgain` branch, where `_hasStoppedRolling` is reset to `false`) → `YumYumForcedRerollEvent` when an opponent successfully blocks the stop by playing Yum Yum.
- `GameSession.GameEnd.cs`: `FinalizeGameEnd` → `GameEndedEvent`; bust path → `PlayerBustedEvent`.
- `TokenPhase/Services/TokenPhaseTokenResolver.cs`: token draw/stash points → `TokenDrawnEvent`, `CardStashedEvent`, `CardDrawnPrivatelyEvent`, via a small public `GameSession.RecordLogEvent(evt)` wrapper (these services already hold a `GameSession` reference).
- `TokenPhase/Services/TokenPhaseBanditHandler.cs`: Bandit reveal/stash → `CardDrawnFaceUpEvent`, `CardStashedEvent(WasFaceUp: true)`.

**Event-timing note (resolved):** a voluntary stop is a two-stage process, and the log models both stages separately rather than trying to make one event carry an accurate count too early.
1. `TryRequestVoluntaryStop` immediately emits `TurnStoppedRollingEvent` ("Rita Raccoon stopped rolling.") — this is honest about what actually happened at that instant: the player *requested* to stop and the Yum Yum window opens. No card count is claimed here because none is known yet.
2. From there, one of two things happens: an opponent plays Yum Yum in `TryYumYumRespond`, which forces another roll — emit `YumYumForcedRerollEvent` ("Bandit BJ played Yum Yum on Rita Raccoon — she must roll again!") and the turn continues in RollPhase, no further "stop" event follows (the stop didn't stick). Or nobody blocks, the stop stands, TokenPhase resolves the collected tokens one at a time — each token's own resolution already emits its own event as it happens (`CardStashedEvent` for StashTrash/DoubleStash, `StealCompletedEvent`/`StealBlockedEvent`/`StealRoleSwappedEvent` for a Steal token, etc.) — and once TokenPhase finishes, `TokenPhaseCoordinator.CompleteTokenPhaseAndEndTurn` emits a plain `TurnResolvedEvent` ("Rita Raccoon finished her turn.") with no count of its own, since the per-token events already told that story as it happened.

This avoids ever inventing a summary number that duplicates what the individual token-resolution events already reported, while still giving the log an immediate "stopped rolling" line to react to (matching the screenshot's per-action granularity) and correctly capturing the Yum Yum block as its own visible event.

### 3. Per-viewer redaction — new `GameLogProjector` (internal, alongside `GetViewForPlayer`)

Raw events now carry full card identity (see §1), so the projector — not the event shape — is the sole enforcement point for hidden information; every viewer sees the same count/order of entries, only `Message` text differs, which is the invariant that guarantees log completeness isn't accidentally broken by redaction:
- Face-down `CardStashedEvent` → card identity shown only to the acting player ("You stashed 3 Feesh" vs "Rita Raccoon stashed 1 card"), matching the screenshot's own asymmetry.
- Face-up (Bandit) stash/draw → identity is already public per existing `GameView.OpponentSummaryView.StashFaceUpCards`/`TokenPhaseView.BanditRevealedName` semantics — show to everyone.
- `StealCompletedEvent` → third parties see no card identity; the victim sees "your card was taken" framing; the thief sees the actual card (it's now visibly in their own `GameView.HandCards`, so nothing new is leaked beyond what that same view call already exposes).
- Token draws are never secret (publicly visible token trays) — show to everyone.

Output type: `GameLogEntryView(int SequenceNumber, int TurnNumber, int ActingPlayerSeat, string Message)` — a single pre-rendered string per entry, not a structured template+args. `ActingPlayerSeat` is included (not just derivable from `Message` text) specifically because the design reference (see §6) color-codes each log entry by its actor — the frontend needs the seat to look up that player's already-established color assignment (reused from wherever opponent tiles assign colors today), not parse it out of a rendered string. (Deferred: i18n-able structured messages, entry merging/summarization — the reference design's examples are all 1:1 with single actions, so no aggregation is built.) Player names resolved at projection time from `players[seat].Name`, not baked into stored events (names are presentation, not domain fact — keeps events replay-safe later).

### 4. Storage & exposure — new field on `GameView`, not a new endpoint

Add `IReadOnlyList<GameLogEntryView> Log` directly to the existing `GameView` record (`TrashAnimal/GameView.cs`), populated in `GetViewForPlayer` via `GameLogProjector.BuildForViewer(_logRecorder.Events, playerIndex, _players)`.

Rejected alternative: a separate `GET /games/{id}/log` endpoint. It would force either a second REST fetch on every SignalR-triggered update, or a second cache-invalidation path in `useGameSignalR`/TanStack Query kept in lockstep with the view fetch — for no isolation benefit, since the log needs exactly the same per-viewer redaction and exactly the same real-time refresh cadence as the rest of `GameView`. Piggybacking on `GameView` means **zero changes** to `GameCommandResponse`, `PlayerViewResponse`, `GameCreationResponse`, `GamesController`, `GameHub`, or `GameUpdateEnvelope` — it flows through automatically. `GameUpdateEnvelope` stays trigger-only exactly as documented ("never trust the envelope as authoritative state") — no new field needed there either.

Trade-off: every `GameView` payload grows by the full accumulated log, every fetch, for the life of the game — acceptable at expected game lengths (dozens of turns), same accepted trade-off as the unbounded in-memory list.

**Backlog note (not this pass):** revisit this trade-off later by shifting responsibility around rather than treating it as all-or-nothing. Candidate directions to explore together when it matters: (a) `GameView.Log` only carries the N most-recent entries per fetch, with the frontend accumulating/caching the full history client-side across fetches (still never the source of truth — just a running local copy); (b) bring back a dedicated `GET /games/{id}/log` endpoint specifically as a *recovery* path — e.g. if the frontend's local accumulated copy is lost (hard refresh, new device) or the user scrolls back further than what's been accumulated locally, fetch older entries or the full history on demand; (c) some combination of (a) and (b) — small tail on every `GameView` fetch for real-time display, on-demand endpoint for backfill/deep scroll-back. None of this is needed until payload size or lost-history actually becomes a problem in practice.

### 5. Frontend

There is an existing high-fidelity design reference for this exact panel — see §6 — which both this section and the design pass should treat as the primary source, more authoritative than the original screenshot (the screenshot is a rendered view of this same mockup).

- `TrashAnimal.Web/src/api/types.ts`: add `GameLogEntryView { sequenceNumber, turnNumber, actingPlayerSeat, message }` and a `log: GameLogEntryView[]` field on the existing `GameView` interface.
- **No new data-fetching hook required** — `useGameView(gameId, playerSeat)` (existing TanStack Query hook) already returns `data.view.log` once the type is added; SignalR's existing `onGameUpdated → invalidateQueries → refetch` flow picks up new entries automatically. Optionally add a thin derived selector `useGameLog.ts` for ergonomics — not required for correctness.
- `TrashAnimal.Web/src/pages/GameBoardPage.tsx`: destructure `gameView.log` at the existing `const { view: gameView, allowedActions } = ...` point, pass to a new `GameLogPanel` component alongside the other `components/gameboard/*` components.
- New file `TrashAnimal.Web/src/components/gameboard/GameLogPanel.tsx` — props `{ entries: GameLogEntryView[] }`. Per the mockup's reference logic (`mainView_desktop.html`, lines ~168-176 — treat as pseudocode for interaction logic, not literal code to paste in, per that bundle's own README caveat): render `entries` in chronological order (oldest first) inside a `flex-direction: column-reverse` scroll container — this puts the newest entry visually at the top without any client-side re-sorting on each update. Each row: a small colored dot keyed off `entry.actingPlayerSeat` (reusing whatever color-assignment logic already exists for opponent tiles elsewhere in `GameBoardPage`/`OpponentRail` — do not invent a new color scheme) + a two-line text block (message, then "Turn {turnNumber}" beneath it). **Markup/styling precision is out of scope for this implementation pass** — build the data-driven seam (props in, the column-reverse scroll structure, minimal list markup so the feature is testable end-to-end) and hand off pixel-level styling to the design pass described below; the frontend engineer should still follow the mockup's structural shape (dot + two-line entry, reverse-scroll container) since that's a functional/behavioral requirement, not just visual polish.

### 6. Design handoff

**A high-fidelity design reference for this exact panel already exists** at `TrashAnimal/.claude/docs/plans/design_handoff_main_game_view/` (`README.md` + `mainView_desktop.html`, with supporting assets/screenshots) — this supersedes the original screenshot as the source of truth; the screenshot is simply a rendered view of this same mockup. Per that bundle's own README: fidelity is **high** ("colors, typography, spacing, sizes, and interaction states... are final — implement pixel-close"), the design canvas is a fixed 1920×1080 with absolutely-positioned layers, and the HTML file is a design-tool export to reference for exact values, not to copy verbatim — recreate it using the codebase's actual component/styling patterns.

Exact specs for the game log panel, straight from that reference (`README.md` "Game log (top-right)" section and the corresponding markup in `mainView_desktop.html`):
- Position: `right:28px; top:110px; bottom:523px; width:260px` (fixed height region matched to the opponent rail's vertical extent).
- Container: glass panel matching the opponent-tile styling — `background: rgba(10,16,32,.55)`, `backdrop-filter: blur(6px)`, `border: 1px solid rgba(255,255,255,.15)`, `border-radius: 16px`, `box-shadow: 0 6px 16px rgba(0,0,0,.3)`.
- Header: "GAME LOG", `#cfd8e8`, 13px/600 weight, letter-spacing `.12em`.
- List: `overflow-y: auto`, `flex-direction: column-reverse`, `gap: 10px` — rendered from a chronologically-ordered array so new entries appear at the top without re-sorting (see §5 for the corresponding frontend behavior this implies).
- Each entry: an 8px circular dot colored by the acting player (`entry.color` in the mockup — in the real implementation, derived from `actingPlayerSeat` via the existing per-player color assignment, not a new field from the backend) + a two-line text block: message `#e8ecf4` at 13px, "Turn N" timestamp-style line `#7c88a9` at 11px beneath it.
- Font: Fredoka (500/600/700, Google Fonts), matching the rest of the screen.

Treat this mockup bundle as ground truth and match it pixel-close, per its own stated fidelity level — no further design exploration is needed beyond faithfully recreating what's already specified there.

### 7. Testing

- `TrashAnimal.Tests/GameLog/GameLogEmissionTests.cs` — both the RollPhase-handler path and the `ApiSupport` explicit-choice path emit equivalent events (catches drift between the duplicated paths); full TokenPhase resolution produces one event per sub-step in increasing `SequenceNumber` order; bust/turn-end/game-end emit the right event; Doggo block vs Kitteh swap vs completed steal emit distinct event types.
- `TrashAnimal.Tests/GameLog/GameLogProjectorRedactionTests.cs` — face-down stash reveals identity only to the actor; face-up stash reveals to all; steal completion reveals correctly per viewer role (actor/victim/third-party); **every viewer sees the same `SequenceNumber` set** (only `Message` differs) — this is the key invariant test. Additionally: assert the *raw* `GameLogEvent` (pre-projection) always carries full `CardId`/`CardName` identity regardless of viewer, confirming redaction is enforced only at `GameLogProjector`, not by withholding data at emission — this is what keeps the door open for a future replay consumer while guaranteeing today's `GameLogEntryView.Message` output never leaks it.
- `TrashAnimal.Api.Tests/Contract/GameLogContractTests.cs` — `GameView.log` round-trips through JSON with camelCase matching `types.ts`.
- `TrashAnimal.Api.Tests/Integration/GameLogIntegrationTests.cs` — using existing `GameApiClient` + `SequencedDie` helpers (no repository mocking, per project convention): drive a turn through `POST /games/{id}/commands`, assert `GET /games/{id}/view?playerSeat=X` shows correctly-redacted, differently-worded log lines per seat for the same underlying turn; log accumulates across multiple commands within a game.
- `TrashAnimal.Web/src/api/contracts.test.ts` — extend for the new `log` field.

## Explicit Scope Boundary (deferred, not this pass)

- Persistence, durable storage, resume/rejoin, replay logic. The event shape is designed to make this additive later (sequence numbers, structured IDs not display strings) but nothing beyond the in-memory list is built now.
- Pruning/pagination of the log for very long games.
- i18n/structured message templates (server sends fully-rendered strings).
- Entry merging/summarization (one event = one log line, no aggregation).
- The command-sourcing/deck-order-capture alternative discussed for gap 2 — not decided yet, revisit before a resume feature is built.

## Critical Files

- `TrashAnimal/GameSession.cs`, `GameSession.StealYumRoll.cs`, `GameSession.ApiSupport.cs`, `GameSession.GameEnd.cs` — emission hooks, `TurnNumber` counter, `RecordLogEvent`/`LogEvents` accessor
- `TrashAnimal/GameView.cs` — add `Log` field
- `TrashAnimal/TokenPhase/Services/TokenPhaseTokenResolver.cs`, `TokenPhaseBanditHandler.cs`, `TokenPhase/TokenPhaseCoordinator.cs` — emission hooks
- New: `TrashAnimal/GameLog/GameLogEvent.cs`, `IGameLogRecorder.cs`, `GameLogRecorder.cs`, `RollPhaseLogEventFactory.cs`, `GameLogProjector.cs`
- `TrashAnimal.Web/src/api/types.ts`, `TrashAnimal.Web/src/pages/GameBoardPage.tsx`
- New: `TrashAnimal.Web/src/components/gameboard/GameLogPanel.tsx`
- Reference (read, don't modify): `TrashAnimal/.claude/docs/plans/design_handoff_main_game_view/README.md` + `mainView_desktop.html` — high-fidelity design source for `GameLogPanel`'s exact visual spec (see §6)

## Verification

1. `dotnet test TrashAnimal.Tests --filter "FullyQualifiedName~GameLog"` and `dotnet test TrashAnimal.Api.Tests --filter "FullyQualifiedName~GameLog"` — new tests pass, existing suite unaffected.
2. `dotnet run --project TrashAnimal.Api`, then in `TrashAnimal.Web` run the dev server; play a few turns across 2+ simulated players (or via the CLI harness `dotnet run --project TrashAnimal` for a quick domain-level sanity check) and confirm `GameView.log` in the API response (via `/scalar/v1` or browser devtools network tab) grows correctly and shows different `message` text per `playerSeat` for actions involving hidden information (stash, steal).
3. In the browser, confirm the log panel renders entries in order, updates in real time when another simulated player acts (via SignalR-triggered refetch), and auto-scrolls to the newest entry, and visually matches the reference screenshot.

## Note on Plan Document Location

Per project preference, plan documents should live at `TrashAnimal\.claude\docs\plans` going forward. Once this plan is approved, copy it to `TrashAnimal\.claude\docs\plans\game-log-feature.md` as the first implementation step.
