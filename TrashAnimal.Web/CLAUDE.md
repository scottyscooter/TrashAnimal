# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in `TrashAnimal.Web`. See the [repo-root CLAUDE.md](../CLAUDE.md) for backend/domain context.

## Project Overview

**TrashAnimal.Web** is the browser client for the TrashAnimal card game. Routing exists (see Testing Notes below) and the API/SignalR services layer is now built (see Backend Integration below). Home and Lobby are real, backend-integrated pages (Task 3 of the Playable Browser UI plan); GameBoard and Results are still placeholder content pending Tasks 4+.

This is a plain Node/npm project, not a .NET project. It is **not** part of `TrashAnimal.slnx` (the dotnet solution file) — it lives alongside the backend projects in the same repo but builds and runs independently via `npm`.

**Styling: Tailwind CSS v4**, wired via `@tailwindcss/vite` in `vite.config.ts` and `@import 'tailwindcss';` at the top of `src/index.css`. Existing hand-written global CSS (custom properties for color/typography, light/dark via `prefers-color-scheme`) lives alongside it in the same file — new component styling should prefer Tailwind utility classes.

## Stack

- **Vite** (v8) — dev server + build tooling
- **React 19** + **TypeScript** (~6.0)
- **oxlint** — linting (Rust-based, fast; not ESLint)
- **Vitest** — unit and component testing (Vite-native, fast)
- **React Testing Library** — component testing utilities
- **Playwright** — end-to-end testing
- **React Router** (v7) — client-side routing
- **TanStack Query** (`@tanstack/react-query`) — server state (REST) caching/invalidation
- **`@microsoft/signalr`** — SignalR hub client (notification-only, see Backend Integration below)
- **`msw`** — mocks REST calls in unit tests; does not intercept SignalR transport negotiation
- **Tailwind CSS v4** (`@tailwindcss/vite`) — utility-first styling

## Common Commands

Run from within `TrashAnimal.Web/`:

```bash
npm install          # install dependencies
npm run dev          # start dev server (default: http://localhost:5173)
npm run build        # type-check (tsc -b) and produce a production build in dist/
npm run preview      # serve the production build locally
npm run lint         # run oxlint
npm run test         # run unit/component tests in watch mode (Vitest)
npm run test:ui      # run tests with interactive UI dashboard
npm run test:run     # run tests once and exit (useful for CI)
npm run test:e2e     # run end-to-end tests (Playwright)
```

## Testing

### Navigation

The app uses **React Router** for navigation with these routes:
- `/` — Home (create/join game)
- `/games/:gameId/lobby` — Lobby (wait for game start)
- `/games/:gameId` — Game Board (active gameplay)
- `/games/:gameId/result` — Results (final scoreboard)

Each page has navigation buttons that use `useNavigate()` to move through the flow.

**Unit tests** mock `useNavigate()` to verify buttons call the right paths.
**E2E tests** verify the complete flow: buttons work, URLs change, and content renders for each page.

See `src/pages/*.test.tsx` for unit test examples and `e2e/navigation.spec.ts` for the full flow.

### Unit & Component Tests

Tests are written with **Vitest** (fast, Vite-native) and **React Testing Library** (user-centric component testing). Test files live alongside source code with a `.test.tsx` or `.test.ts` extension.

**File Structure:**
```
src/
  components/
    Button.tsx
    Button.test.tsx     # component test
  pages/
    HomePage.tsx
    HomePage.test.tsx   # page test
  test/
    setup.ts            # Vitest configuration
    test-utils.tsx      # custom render() + shared utilities
    example.test.tsx    # reference test file
```

**Writing Tests:**

Import `render` and utilities from `src/test/test-utils.tsx` (not directly from `@testing-library/react`). This ensures all tests use the same provider/wrapper setup:

```typescript
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '../test/test-utils';
import Button from './Button';

describe('Button', () => {
  it('calls onClick handler when clicked', () => {
    const handleClick = vi.fn();
    render(<Button onClick={handleClick}>Click me</Button>);
    
    fireEvent.click(screen.getByRole('button', { name: /click me/i }));
    expect(handleClick).toHaveBeenCalledOnce();
  });
});
```

**Best Practices:**
- Use `screen` queries instead of destructuring render result (more resilient)
- Query by accessibility role or label, not test IDs (except as last resort)
- Test behavior and output, not implementation details
- Keep individual tests focused and independent
- Use `vi.fn()` for mocking callbacks; avoid mocking components until necessary

**Setup & Configuration:**
- `src/test/setup.ts` — runs before all tests; mocks `window.matchMedia` and cleans up after each test
- `vitest.config.ts` — configures jsdom environment, globals, and coverage
- Add providers (routing, context, Redux, etc.) to `AllProviders` in `test-utils.tsx` as they're introduced

### End-to-End Tests

E2E tests with **Playwright** live in an `e2e/` folder at the root and test full user flows in a real browser.

**File Structure:**
```
e2e/
  auth.spec.ts
  game-flow.spec.ts
  navigation.spec.ts
```

**Running E2E Tests:**

```bash
npm run test:e2e                # run all e2e tests
npx playwright test e2e/game-flow.spec.ts  # run one suite
npx playwright test --debug     # interactive debug mode
npx playwright show-report      # view HTML report
```

**Configuration:**
- `playwright.config.ts` — base URL, browser targets, CI settings, web server config
- Tests auto-start the dev server (`npm run dev`) if not already running
- Default base URL is `http://localhost:5173`

**Writing E2E Tests:**

```typescript
import { test, expect } from '@playwright/test';

test('navigate from home to lobby', async ({ page }) => {
  await page.goto('/');
  await page.click('text=Start Game');
  await expect(page).toHaveURL('/lobby');
});
```

## Backend Integration

The client talks to `TrashAnimal.Api` (see repo-root CLAUDE.md) through a dedicated services/api layer — no component calls `fetch`/SignalR directly.

- `src/api/types.ts` — TS mirrors of every backend DTO and domain enum (`GameView`, `GameAction`, `TokenAction`, `CardName`, `LobbyView`, etc.), camelCase to match ASP.NET Core's default JSON naming policy. `SubmitCommandRequest` is a discriminated union keyed by `kind` (not a flat mirror of the C# record's 5 optional fields) — it models every distinct shape `POST /games/{gameId}/commands` accepts (plain actions, `PlayFeesh`/`PlayShiny`/`ResolveTokenSteal`, and the contextual card-pick/double-stash/recycle-pick requests the backend routes by `GameState`/`TokenPhaseStep` rather than by the action field), so wrong-field mistakes are caught at compile time.
- `src/api/httpClient.ts` — shared `fetch` wrapper + `ApiError`. Tolerates both JSON error bodies (`GamesController`'s 422, a structured `GameCommandResponse`) and bare-text error bodies (`LobbiesController`'s 400/403/409/422). `API_BASE_URL` reads `VITE_API_BASE_URL`, falling back to `http://localhost:5080` (see `TrashAnimal.Api/CLAUDE.md`'s pinned dev port).
- `src/api/gamesApi.ts` — `submitCommand` returns the parsed `GameCommandResponse` (including `succeeded: false`) rather than throwing on 422, since that's `GamesController`'s only expected-rejection status and it always carries the structured envelope.
- `src/api/lobbiesApi.ts` — throws `ApiError` uniformly for its four expected-rejection statuses (400/403/409/422), since `LobbiesController` returns bare strings for all of them and they're form-validation/conflict outcomes rather than a live-game state race. This asymmetry with `gamesApi` is intentional, not an oversight.
- `src/api/signalRClient.ts` — `connectToGameHub`/`connectToLobbyHub` wrap `@microsoft/signalr` with automatic reconnect + group join/leave. `GameHub` is push-only (`GameUpdated` is a trigger to re-fetch via REST, never trusted as state, preserving the per-player hidden-information boundary). `LobbyHub` pushes the full `LobbyView` directly on `LobbyUpdated` (no hidden-information constraint on a seat list). On reconnect, the game hub's caller compares the cached `PlayerViewResponse.Revision` against a fresh fetch before updating; the lobby hub's caller always refetches unconditionally, since `LobbyView` has no `Revision` field — an accepted, documented asymmetry.
- `src/hooks/` — TanStack Query hooks wrapping the above: `useLobby`, `useJoinLobby`, `useStartLobby`, `useCreateLobby`, `useLobbySignalR`, `useGameView`, `useSubmitCommand`, `useGameSignalR`, `useGameResult`. Plus non-API state hooks: `useLocalStorage` (generic, `useSyncExternalStore`-backed so multiple hook instances reading the same key stay in sync within a tab — plain `useState` would leave e.g. `JoinForm` and `LobbyPage` unaware of each other's writes to `trashanimal:identity`), `useClientIdentity` (wraps it for the seat/token identity slot, scoped by lobbyId match), `useTheme` (wraps it for `trashanimal:theme`, stamping `<html data-theme>`). `QueryClientProvider` and `ToastProvider` are wired in `src/main.tsx` (app) and `src/test/test-utils.tsx` (tests, with query retries disabled).
  - **Cache-update convention on mutation success:** default to `invalidateQueries` (forces a fresh GET, so the cache never diverges from the server's shape — `useStartLobby`, `useSubmitCommand`). Use `setQueryData` only where a hook is explicitly documented as filling a gap before a corresponding SignalR push would otherwise refresh it — `useJoinLobby` is the one current exception, since the join response's `lobby` field is already the full authoritative `LobbyView` and writing it directly closes the gap before `LobbyHub`'s `LobbyUpdated` arrives for that seat. New hooks should default to `invalidateQueries` and only reach for `setQueryData` with the same kind of justification, documented in a comment on the hook.
- `src/test/msw/` — shared `msw` scaffold (`server.ts` + `handlers.ts`) wired into `src/test/setup.ts`'s `beforeAll`/`afterEach`/`afterAll`, with default success-path handlers for every endpoint. `signalRClient.ts`'s reconnect/re-join logic isn't testable via `msw` (it intercepts fetch/XHR, not SignalR's transport negotiation) — that's covered instead via `vi.mock('@microsoft/signalr')` in `src/api/signalRClient.test.ts`, backstopped by a real Playwright e2e reconnect test (Task 8).

## Home + Lobby (Task 3)

`HomePage`/`LobbyPage` are real, backend-integrated pages under `/games/:lobbyId/lobby` (the route param is `lobbyId`, not `gameId` — a lobby's ID and the real game's ID it eventually produces are distinct GUIDs).

- `src/components/CreateSessionForm.tsx` — nickname input, calls `useCreateLobby`, persists identity via `useClientIdentity`, navigates to the new lobby. Used by `HomePage`.
- `src/components/JoinForm.tsx`, `PlayerList.tsx`, `ShareLink.tsx`, `StartGameButton.tsx` — used by `LobbyPage`. `StartGameButton` only fires the start mutation; it does not navigate — `LobbyPage` itself watches `lobby.isStarted`/`lobby.gameId` (refreshed via `useStartLobby`'s cache invalidation for the host, and `useLobbySignalR`'s push for every other seat) and navigates every seated player uniformly once the lobby starts.
- `LobbyPage` branches on stored identity: no identity for this lobbyId → `JoinForm`; identity present → seated view (`PlayerList` + `ShareLink`, plus `StartGameButton` only when `identity.seatIndex === 0`, i.e. the host). A returning host/guest who refreshes the page skips `JoinForm` entirely, since `useClientIdentity` already has their seat.
- `src/components/Toast/` — `ToastProvider` (wired in `main.tsx` and `test-utils.tsx`) + `useToast()`. Error-surface convention: form-triggered mutation errors (create/join/start) render inline near the triggering control via `ApiError.message`; connection-level errors (`useLobbySignalR`'s `onConnectionError`) show as a toast. Duplicate-nickname join errors are the one case with extra handling — `JoinForm` substring-matches the known message text (no structured error code exists on `LobbiesController`'s bare-string bodies) and refocuses the nickname field.
- `src/components/ThemeToggle.tsx` — cycles `useTheme`'s `system`/`light`/`dark`, rendered on `HomePage`.

## Enum Contract Pattern

**Single Source of Truth for Enums:** In `src/api/types.ts`, all domain enums (GameState, GameAction, CardName, TokenAction, etc.) are defined once as const arrays with `as const`, and TypeScript types are derived from them:

```typescript
export const GAME_STATE_VALUES = [
  'RollPhase',
  'AwaitingYumYum',
  'AwaitingStealResponse',
  // ...
] as const;
export type GameState = typeof GAME_STATE_VALUES[number];
```

**Why:** This ensures types and runtime validation can never drift. Contract tests import the const arrays and validate backend responses against them—if backend enums diverge, tests fail immediately. The const is the authority; the type is derived.

**Pattern for New Enums:**
1. Define the const array with `as const`
2. Derive the type from it: `typeof ARRAY[number]`
3. Export both
4. Tests import the const for validation
5. Never write type unions manually or create hardcoded lists in tests—always derive from the const

This is **mandatory** for any new enum added to the frontend/backend contract.

## Conventions

Until project-specific conventions are established, follow the same spirit as the backend (see repo-root CLAUDE.md `Code Patterns & Standards`):
- Intention-revealing names; avoid `data`, `info`, `temp`, `helper`
- Keep files small and single-purpose; split by feature/domain area, not just by technical layer
- Avoid premature abstraction — this scaffold has no existing patterns to follow yet, so early code should stay simple until real requirements (routing, state, multiple views) emerge

## Status

Routing, state management (TanStack Query), the API/SignalR client structure, and the styling approach (Tailwind CSS v4) are now decided and built. Page-level UI is still in progress; Task 7 (responsive/accessibility polish) is where the styling approach gets exercised in full rather than decided.
