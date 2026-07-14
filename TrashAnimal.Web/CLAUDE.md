# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in `TrashAnimal.Web`. See the [repo-root CLAUDE.md](../CLAUDE.md) for backend/domain context.

## Project Overview

**TrashAnimal.Web** is the browser client for the TrashAnimal card game. It is currently a **barebones scaffold** — the default Vite + React + TypeScript template with no game-specific code yet. Routing, state management, styling, and API/SignalR integration have not been designed or built.

This is a plain Node/npm project, not a .NET project. It is **not** part of `TrashAnimal.slnx` (the dotnet solution file) — it lives alongside the backend projects in the same repo but builds and runs independently via `npm`.

## Stack

- **Vite** (v8) — dev server + build tooling
- **React 19** + **TypeScript** (~6.0)
- **oxlint** — linting (Rust-based, fast; not ESLint)
- No routing, state management, or HTTP/SignalR client libraries installed yet

## Common Commands

Run from within `TrashAnimal.Web/`:

```bash
npm install       # install dependencies
npm run dev       # start dev server (default: http://localhost:5173)
npm run build     # type-check (tsc -b) and produce a production build in dist/
npm run preview   # serve the production build locally
npm run lint       # run oxlint
```

There are no tests configured yet.

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
