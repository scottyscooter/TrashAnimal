# TrashAnimal Playable Browser UI — Full Vertical Slice

## Context

`TrashAnimal.Web` is currently a barebones scaffold: four placeholder pages (`HomePage`, `LobbyPage`, `GameBoardPage`, `ResultsPage`) wired together with `react-router-dom`, each just a heading and a single nav button. No API client, no state management, no real component library exists yet, though card/token/background art is already dropped into `src/assets/images/`. The goal is to turn this into a fully playable, responsive, accessible multiplayer implementation of the TrashAnimal card/dice game, following the existing page progression (Home → Lobby → GameBoard → Results).

The user wants a genuine multi-device experience: an admin creates a session from the Home page, shares the resulting Lobby URL, other players join with a nickname, and the admin starts the game once 2–4 players are present. **The backend as it stands cannot support this** — `POST /games` requires the complete player roster up front and creates a live `GameSession` immediately; there is no "people are still joining" concept. This was flagged to the user explicitly, along with the related fact that `SubmitCommandRequest.PlayerSeat` is an unauthenticated bare int (no seat-spoofing protection). The user chose to (a) include a real backend Lobby addition in this plan rather than fake the flow client-side, and (b) accept a scoped-down identity mechanism: enough for reconnection, not full per-command authorization — the existing seat-spoofing gap on `/commands` is a known, unchanged limitation carried forward. The user also confirmed: full vertical slice (real API/SignalR integration, not mocked UI), and day/night background is a purely aesthetic, per-browser local toggle (default day), unrelated to game state.

Backend patterns (`IGameSessionRepository`/`InMemoryGameSessionRepository`, `GameSessionEntry` wrapping `GameSession`+`Die`+`Revision`+`SemaphoreSlim` lock, `IGameUpdatePublisher`/`SignalRGameUpdatePublisher`/`StubGameUpdatePublisher`, `GameApplicationService` constructor-injected with repository/publisher/logger/options) were confirmed against actual source, not inferred — the new Lobby slice should mirror this shape.

## A. Backend additions — TrashAnimal.Api Lobby slice

Additive only; `GameSession`, `GameApplicationService`, `GamesController` stay untouched. New parallel vertical slice:

- `TrashAnimal.Api/Lobbies/LobbyEntry.cs` — in-memory entity: `Guid Id`, `List<LobbySeat> Seats` (max 4), `SemaphoreSlim Lock` (mirrors `GameSessionEntry`'s per-entity lock), `int Revision`, `bool IsStarted`, `Guid? GameId`.
- `TrashAnimal.Api/Lobbies/LobbySeat.cs` — `record LobbySeat(int SeatIndex, string Nickname, string ClientToken)`.
- `TrashAnimal.Api/Lobbies/ILobbyRepository.cs` / `InMemoryLobbyRepository.cs` — same shape as `IGameSessionRepository`/`InMemoryGameSessionRepository` (`ConcurrentDictionary<Guid, LobbyEntry>`, `Add`/`TryGet`/`Remove`).
- `TrashAnimal.Api/Application/LobbyApplicationService.cs` — constructor-injects `ILobbyRepository`, `IGameSessionRepository`-consuming `GameApplicationService` (to hand off), `ILobbyUpdatePublisher`, `ILogger<LobbyApplicationService>`. Methods:
  - `CreateLobbyAsync(string adminNickname)` → new `LobbyEntry`, admin = seat 0, returns lobby id + admin's client token + `LobbyView`.
  - `JoinLobbyAsync(Guid lobbyId, string nickname)` → validates not started, seat count < 4, nickname unique (trim, case-insensitive) → new seat + token, publishes `LobbyUpdated`.
  - `GetLobbyViewAsync(Guid lobbyId)` — polling fallback / initial load.
  - `StartLobbyAsync(Guid lobbyId, string requestingClientToken)` → validates requester's token matches seat 0, validates 2–4 seats (replicate the existing three-layer style validation pattern used for game creation: controller-level + service-level checks), calls existing `GameApplicationService.CreateGameAsync(seats.Select(s => s.Nickname).ToList())`, stores `GameId`, marks started, publishes a final lobby update carrying `GameId` so every connected tab can auto-navigate. Lobby seat index is reused directly as the game's `playerSeat` — no remapping.
- Contracts (mirror existing `Contracts/Requests` and `Contracts/Responses` folders):
  - `Contracts/Requests/CreateLobbyRequest.cs` — `record CreateLobbyRequest(string Nickname)`
  - `Contracts/Requests/JoinLobbyRequest.cs` — `record JoinLobbyRequest(string Nickname)`
  - `Contracts/Requests/StartLobbyRequest.cs` — `record StartLobbyRequest(string ClientToken)`
  - `Contracts/Responses/LobbyView.cs` — `record LobbyView(Guid LobbyId, IReadOnlyList<LobbySeatView> Seats, bool IsStarted, Guid? GameId)`; `record LobbySeatView(int SeatIndex, string Nickname)` (never exposes tokens)
  - `Contracts/Responses/LobbyJoinResponse.cs` — `record LobbyJoinResponse(LobbyView Lobby, int SeatIndex, string ClientToken)`
  - `Contracts/Responses/LobbyStartResponse.cs` — `record LobbyStartResponse(Guid GameId)`
- `TrashAnimal.Api/Controllers/LobbiesController.cs` (route base `lobbies`):
  - `POST /lobbies` → 201 `LobbyJoinResponse`
  - `GET /lobbies/{lobbyId:guid}` → 200 `LobbyView` / 404
  - `POST /lobbies/{lobbyId:guid}/players` → 200 `LobbyJoinResponse`, 409 (duplicate nickname / full / already started), 404
  - `POST /lobbies/{lobbyId:guid}/start` → 200 `LobbyStartResponse`, 403 (not admin token), 422 (seat count out of range), 409 (already started)
- `TrashAnimal.Api/Hubs/LobbyHub.cs` — mirrors `GameHub`: `JoinLobbyAsync`/`LeaveLobbyAsync`, group `lobby:{lobbyId}`, event `LobbyUpdated` carrying the full `LobbyView` directly (no hidden-information constraint here, so no notify-then-refetch needed — simpler than `GameHub`). Mounted at `/hubs/lobby`.
- `TrashAnimal.Api/Updates/ILobbyUpdatePublisher.cs` / `SignalRLobbyUpdatePublisher.cs` / `StubLobbyUpdatePublisher.cs` — mirrors the `IGameUpdatePublisher` trio exactly.
- `Program.cs`: additive registrations — `ILobbyRepository` singleton, `ILobbyUpdatePublisher` scoped, `LobbyApplicationService` scoped, `app.MapHub<LobbyHub>("/hubs/lobby")`.

**Identity/reconnection — explicit scope cut:** the lobby validates `ClientToken` only for the admin-only `start` action. Once a game is created, the frontend persists `seatIndex` + `clientToken` in `localStorage` keyed by `gameId` (derived from the `LobbyJoinResponse` each client already received) — this is what makes refresh/reconnect on the GameBoard work. `SubmitCommandRequest.PlayerSeat` remains unauthenticated exactly as today; this plan does not retrofit per-command token checks into `GameApplicationService` (would require threading a token through ~10 methods on a class documented as "keep untouched"). This closes the *new* lobby-hijack vector (someone else clicking Start) without touching the pre-existing, accepted seat-spoofing gap on `/commands`.

**Included in step 1:** `TokenPhaseView.StashableHandCardsForCurrentPrompt` is currently a tuple `(Guid CardId, CardName Name)`, which `System.Text.Json` serializes as `Item1`/`Item2` without a converter. As part of the backend Lobby slice work (step 1), also change this to a named `StashableHandCard(Guid CardId, CardName Name)` record in `TrashAnimal/TokenPhaseView.cs`, so the frontend DTO mirror in step 2 never has to depend on tuple property names. Small, additive, no behavior change.

Verify with `TrashAnimal.Api.Tests/Integration/LobbyFlowTests.cs` (mirroring existing `GameCreationTests.cs`-style integration tests): create → join → duplicate-nickname rejection → full-lobby rejection → start (wrong token rejected, then correct) → resulting `GameId` is a real playable game.

## B. Frontend architecture

**State management: TanStack Query (`@tanstack/react-query`)**, the one new state-management dependency. The domain is "poll/refetch a server-owned view on a push trigger" — SignalR here is explicitly notify-then-refetch, not a state transport — which is TanStack Query's core use case (cache keyed by `['game', gameId, seat]` / `['lobby', lobbyId]`, `invalidateQueries` from the SignalR handler, built-in loading/error/retry). No Redux/Zustand: there's no complex client-only state graph, all authoritative state is server-owned. No plain Context: it doesn't give caching/dedup for free and we'd rebuild that badly by hand across ~6 fetch points.

- `src/main.tsx` — wrap `<App/>` in `QueryClientProvider`.
- `src/test/test-utils.tsx` — add a fresh `QueryClient` (`retry: false`) to `AllProviders`.

**Services/API layer** (`TrashAnimal.Web/src/api/`):
- `src/api/types.ts` — TS mirrors of every backend DTO (`GameView`, `TokenPhaseView`, `StealPhaseView`, `StealPickSlot`, `LobbyView`, etc.), enums as string-literal unions matching `JsonStringEnumConverter` output.
- `src/api/httpClient.ts` — thin `fetch` wrapper, base URL via `import.meta.env.VITE_API_BASE_URL`, typed `ApiError` (status + parsed body) so 404/409/422 are distinguishable by callers.
- `src/api/lobbiesApi.ts` — `createLobby`, `joinLobby`, `getLobby`, `startLobby`.
- `src/api/gamesApi.ts` — `getView(gameId, seat)`, `submitCommand(gameId, request)`, `getResult(gameId)`.
- `src/api/signalRClient.ts` — one wrapper around `@microsoft/signalr` (new dependency): `connectGameHub(gameId, onUpdate)` / `connectLobbyHub(lobbyId, onUpdate)`, `withAutomaticReconnect()`, re-joins group on reconnect, returns a disposer.
- React Query hooks (`src/hooks/`): `useLobby`, `useJoinLobby`, `useStartLobby`, `useLobbySignalR`, `useGameView`, `useSubmitCommand` (mutation → `invalidateQueries` on the view key), `useGameSignalR`, `useGameResult`.

**Local-only UI state** (`src/hooks/useLocalStorage.ts` backing):
- `useTheme.ts` — key `trashanimal:theme`, default `'day'`, drives a `data-theme` attribute consumed by CSS background rules. Independent per browser, never synced.
- `useClientIdentity.ts` — persists `trashanimal:{gameId}:seat` / `...:clientToken` per the reconnection scheme in A.

## C. Component breakdown

New `TrashAnimal.Web/src/components/` folder, feature-organized.

**`components/shared/`:** `Card.tsx` (maps `CardName` → the 8 webps in `assets/images/cards/`, `back.webp` when face-down), `TokenBadge.tsx` (maps `TokenAction` → the 6 token webps), `PlayerBadge.tsx`, `ActionButton.tsx` (wraps a `GameAction`, hidden/disabled unless present in `AllowedActions` — the one control every phase panel is built from), `Modal.tsx` (focus trap, `role="dialog"`, `aria-modal`, restores focus on close), `ThemeToggle.tsx`, `LiveAnnouncer.tsx` (mounted once; `aria-live="polite"` + a second assertive region; exposed via `useAnnouncer` hook/context), `LoadingState.tsx`/`ErrorState.tsx`.

`LiveAnnouncer.tsx` explained: it's a screen-reader-only feature, invisible to sighted players. It renders two empty, visually-hidden (`sr-only`) `<div>`s mounted once near the app root — one `aria-live="polite"`, one `aria-live="assertive"`/`role="alert"`. When assistive tech observes text appear inside a live region, it speaks it automatically, without the user needing to navigate to find it. The `useAnnouncer()` hook exposes an `announce(message, urgency?)` function (via context, so any component can call it) that sets the text of the matching div. `GameBoardLayout` owns a `useEffect` that diffs the incoming `GameView` against the previous render (from `useGameView`/`useGameSignalR`) and calls `announce(...)` on the transitions that matter to a non-sighted player: `CurrentPlayerIndex` changes → "It's Bob's turn"; `IsBusted` flips true → "You busted!" (assertive); `YumYumResponderIndex` becomes your seat → "Alice is asking if you'll play Yum Yum" (assertive); a `StealPhase` appears → "Bob is attempting to steal from you" (assertive if you're the victim, polite otherwise); `TokenPhase.Step` changes → "Resolving Bandit"; `State` becomes `GameEnded` → "Game over — view results." Sighted players already see this information through `TurnPhaseIndicator`/modals, but those visual changes don't get spoken by a screen reader unless routed through a live region — that's the whole reason this component exists.

**HomePage** (rewritten) + `components/home/CreateSessionForm.tsx` — nickname input, "Create Game" → `useMutation(createLobby)` → persist identity → `navigate('/games/:lobbyId/lobby')`. Reuses the existing `:gameId` route param as `lobbyId` pre-start; document this reuse rather than adding new routes.

**LobbyPage** (rewritten) + `components/lobby/`: `ShareLink.tsx` (display/copy joinable URL), `JoinForm.tsx` (shown only if no stored identity for this lobby), `PlayerList.tsx` (live via `useLobby`+`useLobbySignalR`, "(you)"/"(admin)" tags), `StartGameButton.tsx` (admin-only, gated on 2–4 seats; on success all tabs navigate to `/games/:gameId` via the `GameId`-carrying lobby update).

**GameBoardPage** (rewritten, orchestrator only) + `components/gameboard/`:
- `GameBoardLayout.tsx` — responsive shell (see D), diffs `view` between renders to drive `LiveAnnouncer`.
- `TurnPhaseIndicator.tsx`, `DieRollControl.tsx` (Roll/Stop + `PhaseOneTokens` tray), `BustBanner.tsx` (Nanners/Blammo/AbandonBust, `role="alert"`).
- `HandDisplay.tsx` — own hand, reusable in single-select/multi-select/display-only modes (used directly and by several TokenPhase panels below).
- `OpponentPanel.tsx` (face-down stack sized to hand *count* only — never contents) + `StashDisplay.tsx` (shared between own stash and opponents' — face-up entries show real art, others `back.webp`) + `DiscardPileDisplay.tsx` + `TokenCollectionTray.tsx`.
- `ActionBar.tsx` — generic fallback rendering an `ActionButton` per `AllowedActions` entry not already covered by a specific component; this is also the *first* thing built (step 4 below) so the whole game is playable before visuals are layered in.
- `YumYumPrompt.tsx` (`Modal`, shown to the responder).
- `components/gameboard/steal/`: `StealResponsePrompt.tsx` (victim, `AwaitingStealResponse`), `StealCardPickPanel.tsx` (thief, renders `ThiefPickSlots` as clickable `Card`s using `ThiefFacingLabel`), `StealSpectatorBanner.tsx`.
- `components/gameboard/tokenphase/`: `TokenPhasePanel.tsx` (router on `TokenPhaseView.Step`, plus always-available MmmPie/Shiny/Feesh interrupt buttons), `StashTrashChoicePanel.tsx`, `StashTrashPickPanel.tsx`, `DoubleStashPickPanel.tsx`, `BanditResponsePanel.tsx`, `RecycleChoicePanel.tsx`, `TokenStealVictimPicker.tsx`.
- `TurnEndPanel.tsx` — single `EndTurn` button.

**ResultsPage** (rewritten) + `components/results/`: `ScoreTable.tsx`, `WinnerBanner.tsx` (not color-only — for now render a 🏆 emoji next to the winner's name/text badge; structure the emoji behind a small `WinnerIcon` prop/slot so it's a one-line swap for a real icon image later, not a rewrite), `PlayAgainButton.tsx` (clears stored identity, navigates home).

## D. Responsive layout

Single breakpoint, `768px`, mobile-first CSS, plain per-component CSS files (no CSS-in-JS/Tailwind — stays consistent with current plain-CSS approach).

- **Mobile:** single-column stack (turn indicator → active phase panel only → horizontally-scrollable `HandDisplay` → accordion `OpponentPanel` list → sticky bottom action/theme bar). `Modal` becomes a full-screen sheet.
- **Desktop:** board layout — center column (die/token tray/discard/active phase panel), left column (own hand + stash, docked near the player), right column (opponents, grid 1–3 wide by player count). `Modal` becomes a centered dialog.
- Same component tree both ways — `GameBoardLayout` only changes the CSS grid template at the breakpoint, no duplicated mobile/desktop trees. Card/token `<img>`s use explicit `width`/`height` + `object-fit: contain` to avoid layout shift across sizes.

## E. Accessibility

- Real `<button>` elements throughout (`ActionButton`, interactive `Card`) — never click-handlers on `<div>`/`<img>`.
- `<main>` per page, `<section aria-labelledby>` per board zone (hand/opponents/center).
- `Modal.tsx`: focus trap, focus-restore-on-close, `role="dialog"`/`aria-modal` — used for Yum Yum/steal/TokenPhase prompts.
- Opponent hand: `aria-label="{name} has {count} cards"` on the stack container only — no per-card labels, nothing implying visible content.
- `LiveAnnouncer`: `aria-live="polite"` for informational updates (turn change, token resolved), a separate assertive region for time-sensitive prompts directed at the current viewer (Yum Yum, steal response, bust).
- Focus moves programmatically to the first actionable control when a prompt becomes relevant to the current viewer, not just visually revealed.
- Contrast check against both `day`/`night` backgrounds specifically (photographic backgrounds may need a semi-opaque panel behind text) — treat as an explicit task in the polish pass, not an assumption.
- Forms: `<label htmlFor>`, errors tied via `aria-describedby`. `ThemeToggle` uses `aria-pressed`.

## F. Testing plan

**Unit/component (Vitest + RTL):**
- `src/api/*` against `msw` (new dev dependency — realistic request/response mocking reusable across API-layer and component tests).
- `useLocalStorage`/`useTheme`/`useClientIdentity` — pure logic.
- Every `components/shared/*` in isolation.
- Every TokenPhase/steal panel — hand-built `GameView` fixtures per `GameState`/`TokenPhaseStep` combination in `src/test/fixtures/gameViews.ts`; assert correct controls render and clicking submits the right payload. This is the primary place game-logic-adjacent UI correctness is verified without a live backend.
- Page-level tests extend the existing mocked-`useNavigate`/`useParams` pattern, now also mocking the API/hooks layer.

**E2E (Playwright), against a real running API:**
- Add an API `webServer` entry (or documented pre-step) to `playwright.config.ts` alongside the existing Vite one.
- `e2e/lobby-flow.spec.ts` — create → second context joins → admin sees live update → start → redirect.
- `e2e/happy-path-turn.spec.ts` — two `browser.newContext()`s (the natural Playwright mechanism for independent players, since contexts don't share `localStorage`) through create→join→start→one full turn, asserting SignalR-driven updates land without manual reload.
- `e2e/steal-and-bandit-flow.spec.ts` — needs a second player's action to unblock the first; use Playwright's `request` fixture to script setup quickly via the real REST API, then switch to UI interaction only for the assertions that matter (prompt appears, buttons work) — avoids threading test-only seeding (e.g. `DieSeed`) through production endpoints.
- `e2e/results-flow.spec.ts` — drive a 2-player game to `GameEnded` via the `request` fixture, load `ResultsPage`, assert score table/winner/play-again.
- Update `e2e/navigation.spec.ts` once routes carry real IDs instead of `demo-game` (do this alongside step 3 below).

## G. Build sequencing — task breakdown (source for Todoist project)

This breakdown is the direct source for a Todoist project (one task per numbered item, subtasks as listed, `tags` shown per item). Once the plan is approved, this becomes a batch upload: one new Todoist project, 9 tasks, subtasks under each.

**Task 1 — Backend Lobby slice** · tags: `backend`
Add the parallel Lobby vertical slice described in section A: `Lobbies/LobbyEntry.cs`, `LobbySeat.cs`, `ILobbyRepository`/`InMemoryLobbyRepository`; `Application/LobbyApplicationService.cs`; request/response contracts (`CreateLobbyRequest`, `JoinLobbyRequest`, `StartLobbyRequest`, `LobbyView`, `LobbySeatView`, `LobbyJoinResponse`, `LobbyStartResponse`); `Controllers/LobbiesController.cs`; `Hubs/LobbyHub.cs`; `Updates/ILobbyUpdatePublisher` trio; `Program.cs` DI wiring. Keep `GameSession`/`GameApplicationService`/`GamesController` untouched — additive only.
- Subtask: Implement `LobbyEntry`/`LobbySeat`/`ILobbyRepository`/`InMemoryLobbyRepository` mirroring `GameSessionEntry`/`IGameSessionRepository` patterns.
- Subtask: Implement `LobbyApplicationService` (create/join/get/start), including the admin-token check on start and the 2–4 seat validation.
- Subtask: Add Lobby request/response contract records under `Contracts/Requests` and `Contracts/Responses`.
- Subtask: Implement `LobbiesController` with the 4 routes and status codes described in section A.
- Subtask: Implement `LobbyHub` + `ILobbyUpdatePublisher`/`SignalRLobbyUpdatePublisher`/`StubLobbyUpdatePublisher`, mount at `/hubs/lobby`.
- Subtask: Change `TokenPhaseView.StashableHandCardsForCurrentPrompt` from a `(Guid, CardName)` tuple to a named `StashableHandCard(Guid CardId, CardName Name)` record so it serializes with real property names.
- Subtask: Write `TrashAnimal.Api.Tests/Integration/LobbyFlowTests.cs` covering create → join → duplicate-nickname rejection → full-lobby rejection → start (wrong token rejected, then correct) → resulting `GameId` is playable. Tag also `test`.

**Task 2 — Frontend services/api layer** · tags: `frontend`
Install `@tanstack/react-query`, `@microsoft/signalr`, `msw`. Build the typed API/services layer so nothing later has to hand-roll fetch/SignalR logic.
- Subtask: `src/api/types.ts` — TS mirrors of all backend DTOs (game + lobby), enums as string-literal unions.
- Subtask: `src/api/httpClient.ts` — fetch wrapper + typed `ApiError`.
- Subtask: `src/api/lobbiesApi.ts` and `src/api/gamesApi.ts`.
- Subtask: `src/api/signalRClient.ts` — shared hub-connection wrapper with reconnect handling.
- Subtask: React Query hooks in `src/hooks/` (`useLobby`, `useJoinLobby`, `useStartLobby`, `useLobbySignalR`, `useGameView`, `useSubmitCommand`, `useGameSignalR`, `useGameResult`).
- Subtask: Wire `QueryClientProvider` into `src/main.tsx` and `src/test/test-utils.tsx`.
- Subtask: Unit-test the API layer against `msw` mocks. Tag also `test`.

**Task 3 — Home + Lobby pages, real flow** · tags: `frontend`
- Subtask: Rewrite `HomePage.tsx` + `components/home/CreateSessionForm.tsx` (nickname entry, create lobby, persist identity, navigate).
- Subtask: Rewrite `LobbyPage.tsx` + `components/lobby/{ShareLink,JoinForm,PlayerList,StartGameButton}.tsx`.
- Subtask: `useLocalStorage`, `useClientIdentity`, `useTheme` hooks.
- Subtask: Update `e2e/navigation.spec.ts` and add `e2e/lobby-flow.spec.ts` for the real create→join→start flow. Tag also `test`.

**Task 4 — GameBoard skeleton (playable, unstyled)** · tags: `frontend`
Goal: a complete game is playable end-to-end through generic buttons before any visual polish.
- Subtask: `GameBoardPage.tsx` rewrite as orchestrator (resolve seat, drive `useGameView`/`useGameSignalR`).
- Subtask: `GameBoardLayout.tsx` shell.
- Subtask: `ActionBar.tsx` — generic `ActionButton` per `AllowedActions` entry.
- Subtask: `TurnPhaseIndicator.tsx`, `HandDisplay.tsx` (text-only acceptable at this stage).
- Subtask: Manual smoke test — play one full game start to finish using only the skeleton UI.

**Task 5 — Phase-specific UI** · tags: `frontend`
Layer in real phase UI in turn order, replacing generic `ActionBar` entries as each is covered.
- Subtask: `DieRollControl.tsx`, `BustBanner.tsx`.
- Subtask: `TokenPhasePanel.tsx` router + `StashTrashChoicePanel`, `StashTrashPickPanel`, `DoubleStashPickPanel`, `BanditResponsePanel`, `RecycleChoicePanel`, `TokenStealVictimPicker`.
- Subtask: `YumYumPrompt.tsx`.
- Subtask: Steal subflow — `StealResponsePrompt.tsx`, `StealCardPickPanel.tsx`, `StealSpectatorBanner.tsx`.
- Subtask: `TurnEndPanel.tsx`.
- Subtask: Card/token art components — `Card.tsx`, `TokenBadge.tsx`, `StashDisplay.tsx`, `DiscardPileDisplay.tsx`, `OpponentPanel.tsx`, `TokenCollectionTray.tsx`, `PlayerBadge.tsx`, `Modal.tsx`.

**Task 6 — Results page** · tags: `frontend`
- Subtask: `ResultsPage.tsx` rewrite.
- Subtask: `ScoreTable.tsx`, `WinnerBanner.tsx` (🏆 emoji behind a swappable icon slot), `PlayAgainButton.tsx` (clears identity, navigates home).

**Task 7 — Responsive + accessibility polish pass** · tags: `frontend`, `accessibility`
- Subtask: Breakpoint CSS (768px, mobile-first) across `GameBoardLayout` and per-component stylesheets per section D.
- Subtask: `LiveAnnouncer.tsx` + `useAnnouncer` implementation and wiring into `GameBoardLayout`'s diff effect (see the explanation above) for all transitions listed in section E.
- Subtask: Focus management — move focus to the first actionable control when a prompt becomes relevant to the current viewer.
- Subtask: Contrast check against both `day`/`night` backgrounds; add a semi-opaque text panel if needed.
- Subtask: Keyboard-only pass through a full turn (no mouse) to verify Tab order and activation.
- Subtask: Form labeling/`aria-describedby` pass on `CreateSessionForm`/`JoinForm`; `aria-pressed` on `ThemeToggle`.

**Task 8 — E2E coverage + CI** · tags: `test`
- Subtask: Add API `webServer` entry to `playwright.config.ts` alongside the existing Vite one.
- Subtask: `e2e/happy-path-turn.spec.ts` (two browser contexts, full turn, SignalR-driven updates).
- Subtask: `e2e/steal-and-bandit-flow.spec.ts` (two contexts; use Playwright's `request` fixture to script setup via REST, UI only for the assertions that matter).
- Subtask: `e2e/results-flow.spec.ts` (drive to `GameEnded` via `request` fixture, assert results page).
- Subtask: Wire the expanded e2e suite into CI alongside the existing frontend test job.

**Task 9 — Documentation** · tags: `documentation`
- Subtask: Update `TrashAnimal.Web/CLAUDE.md` — routing param reuse (`:gameId` as lobby id pre-start), TanStack Query decision + rationale, services/api layer structure, seat-token reconnection scheme, theme storage, remove stale "provisional" framing.
- Subtask: Update `TrashAnimal.Api/CLAUDE.md` — new Lobbies section, `LobbyHub`, the accepted seat-spoofing/identity scope cut.

## Verification

- Backend: `dotnet test TrashAnimal.Api.Tests --filter "FullyQualifiedName~LobbyFlowTests"`, then `dotnet test` for the full suite.
- Frontend unit: `npm run test:run` inside `TrashAnimal.Web`.
- Frontend e2e: `npm run test:e2e` with both `TrashAnimal.Api` and the Vite dev server running (per updated `playwright.config.ts`).
- Manual: run `dotnet run --project TrashAnimal.Api` + `npm run dev`, open two browser windows (or one normal + one incognito, to get separate `localStorage`), walk through create → join → start → a full turn including at least one steal and one Yum Yum window → end game → verify results page, on both mobile-width and desktop-width viewports, and with a screen reader or keyboard-only pass through one full turn.

### Critical files referenced
- `TrashAnimal.Api/Application/GameApplicationService.cs`, `TrashAnimal.Api/Controllers/GamesController.cs`, `TrashAnimal.Api/Sessions/{IGameSessionRepository,InMemoryGameSessionRepository,GameSessionEntry}.cs`, `TrashAnimal.Api/Updates/{IGameUpdatePublisher,SignalRGameUpdatePublisher,StubGameUpdatePublisher}.cs`, `TrashAnimal.Api/Hubs/GameHub.cs`, `TrashAnimal.Api/Program.cs`
- `TrashAnimal.Web/src/App.tsx`, `TrashAnimal.Web/src/main.tsx`, `TrashAnimal.Web/src/pages/*.tsx`, `TrashAnimal.Web/src/test/test-utils.tsx`
- `TrashAnimal/GameView.cs`, `TrashAnimal/TokenPhaseView.cs`, `TrashAnimal/StealPhaseView.cs`
