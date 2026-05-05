---
name: trashanimal_api_phase1_host_and_app_layer
overview: Create the TrashAnimal.Api ASP.NET Core project and the GameApplicationService orchestration layer that sits between HTTP and the TrashAnimal engine.
todos:
  - id: create-api-project
    content: Add TrashAnimal.Api as a new ASP.NET Core Web API project referencing TrashAnimal
    status: completed
  - id: configure-di
    content: Register all TrashAnimal.Api services (GameApplicationService, IGameSessionRepository, GameUpdatePublisher) in the DI container
    status: completed
  - id: configure-logging
    content: Enable and configure the logging provider in TrashAnimal.Api
    status: completed
  - id: create-app-service
    content: Implement GameApplicationService with session resolve, player validation, command execution, and update publishing
    status: completed
  - id: configure-json
    content: Configure JsonStringEnumConverter globally in TrashAnimal.Api
    status: completed
isProject: false
---

# Phase 1: Introduce TrashAnimal.Api and Application Layer

## Goal

Stand up the new host project and a single application-layer service that all controllers will delegate to. No game rules live here — all rule decisions stay inside `TrashAnimal`.

## Tasks

### 1.1 Create TrashAnimal.Api Project

- Add a new ASP.NET Core Web API project named `TrashAnimal.Api` to the solution.
- Add a project reference from `TrashAnimal.Api` to `TrashAnimal` (the engine).
- Remove the CLI `Program.cs` as the runtime host; `TrashAnimal.Api` is the new entry point.

### 1.2 Configure Dependency Injection

Register all `TrashAnimal.Api` services in `Program.cs` using `builder.Services`:

- `IGameSessionRepository` → `InMemoryGameSessionRepository` (scoped or singleton as appropriate for shared in-memory state).
- `GameApplicationService` → scoped.
- `GameUpdatePublisher` → scoped (depends on SignalR `IHubContext`, registered in Phase 5 but stub the interface now).
- `Die` → transient or per-game factory; inject `Random` instance to keep randomness testable.

All engine types (`GameSession`, `Deck`, etc.) are created by the application layer, not registered directly in DI.

### 1.3 Enable Logging Provider

- Use the default ASP.NET Core logging pipeline (`builder.Logging`); no third-party provider required for milestone 1.
- Ensure structured logging is configured: log level `Information` for normal game events, `Warning` for rejected commands, `Error` for unexpected faults.
- Inject `ILogger<T>` into `GameApplicationService` and log each command attempt (action, `gameId`, `playerSeat`) and its outcome (accepted / rejected / error).

### 1.4 Create GameApplicationService

Create `GameApplicationService` in `TrashAnimal.Api` as the single orchestration point between HTTP and the engine:

- Resolve `GameSession` from `IGameSessionRepository` by `gameId`.
- Validate that the requesting player's `playerSeat` matches the action being submitted.
- Execute the engine command via `GameSession.ApplyAction(...)` or the appropriate `Try*` method.
- Return the latest `GameView` + allowed actions as a response DTO.
- Publish a realtime update event via `GameUpdatePublisher` after each successful mutation.

### 1.5 Configure JSON Serialization

- Register `JsonStringEnumConverter` globally in `Program.cs` / `builder.Services` so all engine enums (`GameAction`, `GameState`, `TokenAction`, `CardName`, etc.) serialize as strings across all endpoints and SignalR messages.

## Constraints

- `TrashAnimal.Api` calls the engine; it never re-implements game rules.
- All controllers are thin — they parse the request, call `GameApplicationService`, and return the result.
