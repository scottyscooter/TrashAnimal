---
name: trashanimal_api_phase3_session_lifecycle
overview: Add IGameSessionRepository and per-session concurrency guardrails in TrashAnimal.Api to support safe concurrent HTTP requests and future multi-game expansion.
todos:
  - id: session-repository
    content: Define IGameSessionRepository and implement an in-memory version
    status: pending
  - id: concurrency-guard
    content: Add per-session lock or queue in GameApplicationService to prevent concurrent mutation
    status: pending
  - id: game-id-routing
    content: Introduce gameId and playerSeat as first-class route/context values on all endpoints
    status: pending
isProject: false
---

# Phase 3: Session Lifecycle and Concurrency Guardrails

## Goal

Give `TrashAnimal.Api` a proper session store and protect each `GameSession` from concurrent mutation, while introducing `gameId`/`playerSeat` identifiers that make multi-game support additive later.

## Tasks

### 3.1 Define IGameSessionRepository

Create `IGameSessionRepository` in `TrashAnimal.Api` with at minimum:

- `TryGet(Guid gameId, out GameSession session)` / `Task<GameSession?> GetAsync(Guid gameId)`
- `Add(Guid gameId, GameSession session)` / `AddAsync(...)`
- `Remove(Guid gameId)` / `RemoveAsync(...)`

Provide an `InMemoryGameSessionRepository` as the milestone 1 implementation backed by a `ConcurrentDictionary<Guid, GameSession>`.

### 3.2 Add Per-Session Concurrency Guard

- `GameSession` and its internal collections are not thread-safe.
- In `GameApplicationService`, acquire a per-`gameId` `SemaphoreSlim` (or equivalent) before calling any mutating engine method (`ApplyAction`, `Try*`), and release it after.
- Store the semaphores in a `ConcurrentDictionary<Guid, SemaphoreSlim>` co-located with or managed by the repository.

### 3.3 Introduce gameId and playerSeat on All Routes

- Every command endpoint must accept `gameId` (route parameter) and `playerSeat` (from request body or claim).
- Structure routes as `POST /games/{gameId}/commands` and `GET /games/{gameId}/view` from day one — even with one game — so multi-game support requires no route changes later.
- `playerSeat` maps the caller to a `Player` index in the engine; Phase 9 (auth) will replace this with identity resolution.

## Constraints

- `IGameSessionRepository` interface must be compatible with a durable backing store (Redis, SQL) later without changing any controller or application service code.
- Do not expose raw `GameSession` objects over the API boundary; always go through `GameApplicationService`.
