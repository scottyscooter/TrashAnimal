# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in `TrashAnimal.Web`. See the [repo-root CLAUDE.md](../CLAUDE.md) for backend/domain context.

## Project Overview

**TrashAnimal.Web** is the browser client for the TrashAnimal card game. It is currently a **barebones scaffold** — the default Vite + React + TypeScript template with no game-specific code yet. Routing, state management, styling, and API/SignalR integration have not been designed or built.

This is a plain Node/npm project, not a .NET project. It is **not** part of `TrashAnimal.slnx` (the dotnet solution file) — it lives alongside the backend projects in the same repo but builds and runs independently via `npm`.

## Stack

- **Vite** (v8) — dev server + build tooling
- **React 19** + **TypeScript** (~6.0)
- **oxlint** — linting (Rust-based, fast; not ESLint)
- **Vitest** — unit and component testing (Vite-native, fast)
- **React Testing Library** — component testing utilities
- **Playwright** — end-to-end testing
- No routing, state management, or HTTP/SignalR client libraries installed yet

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

## Backend Integration (not yet implemented)

The client will eventually talk to `TrashAnimal.Api` (see repo-root CLAUDE.md):
- REST endpoints under `/games` for all game commands (create game, submit actions, get result)
- A SignalR hub (`GameHub`) for **notification-only** push updates — the client should never submit game actions over SignalR, only listen for `GameUpdated` events and re-fetch/re-render state via REST
- Each player's view is scoped to their own hand; opponent card counts are visible but not identities (`PlayerViewResponse`) — the client must not assume it can see full game state
- Enums are serialized as strings in all JSON payloads from the API

When this integration is built, prefer keeping API/SignalR client code in a dedicated layer (e.g. a `services/` or `api/` module) rather than calling `fetch`/SignalR directly from components, so the domain-shape of the game (turns, phases, steal attempts, Yum Yum windows) can be modeled consistently with the backend's mental model.

## Conventions

Until project-specific conventions are established, follow the same spirit as the backend (see repo-root CLAUDE.md `Code Patterns & Standards`):
- Intention-revealing names; avoid `data`, `info`, `temp`, `helper`
- Keep files small and single-purpose; split by feature/domain area, not just by technical layer
- Avoid premature abstraction — this scaffold has no existing patterns to follow yet, so early code should stay simple until real requirements (routing, state, multiple views) emerge

## Status

This file will need updates as soon as real architecture decisions are made (routing library, state management, API client structure, styling approach). Treat the "Stack" and "Backend Integration" sections above as provisional until then.
