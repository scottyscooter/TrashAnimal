# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**TrashAnimal** is a digital implementation of a card game built in .NET 10. The project consists of:

- **TrashAnimal** — core domain logic (game rules, turn structure, card effects, scoring)
- **TrashAnimal.Api** — REST API + SignalR hub for multiplayer game sessions
- **TrashAnimal.Tests** — xUnit tests for domain game logic
- **TrashAnimal.Api.Tests** — xUnit tests for API contracts and integration scenarios

The codebase targets .NET 10 with nullable reference types enabled (`<Nullable>enable</Nullable>`).

## Architecture

### Layering

**Domain Layer (TrashAnimal/)**
- `GameSession` — orchestrator of turn structure, phase states (RollPhase, AwaitingStealResponse, TokenPhase, GameEnded), and action dispatch
- Game model: `Player`, `Hand`, `Card`, `CardName`, `Deck`, `Die`, `StashPile`
- Game mechanics: steal mechanics (`StealAttempt`), Yum Yum window (`YumYumWindow`), phase handlers (`RollPhaseGameplayHandlerRegistry`)
- Scoring: `GameEndScoreLine`, final result computation

**API Layer (TrashAnimal.Api/)**
- `GamesController` — REST endpoints (create game, get view, submit commands, get result)
- `GameHub` — SignalR push notifications on game state changes; clients join per-game groups and receive `GameUpdated` events
- `GameApplicationService` — bridges HTTP/SignalR to domain; manages in-memory session repository
- Contract DTOs: request (`CreateGameRequest`, `SubmitCommandRequest`) and response shapes (`PlayerViewResponse`, `GameCommandResponse`, `GameResultResponse`)
- Options + validators: `GameApplicationServiceOptions`, `ServiceCollectionExtensions`

**Key Architectural Boundaries**
- Game state flows through `GameSession.GetAllowedActionsForPlayer()` → controller/hub receives validated actions → caller submits a command → `GameApplicationService.DispatchCommandAsync()` calls `GameSession.ApplyAction()` → domain publishes state change event
- SignalR is **notification-only**; all game commands go through REST (`POST /games/{id}/commands`)
- Hidden information: each player sees only their own hand via `PlayerViewResponse`; opponent card counts are visible but not card identities
- Enums serialize as strings across all JSON (see Program.cs: `JsonStringEnumConverter` wired for controllers, minimal APIs, and SignalR JSON protocol)

### GameSession Partial Classes

`GameSession` is split across multiple files using partial classes (contrary to the no-partial-classes rule, this is an exception for logical grouping of a large game coordinator):
- `GameSession.cs` — state machine transitions, turn/phase lifecycle
- `GameSession.ApiSupport.cs` — conversion to `GameView` and action filtering for client
- `GameSession.GameEnd.cs` — end-game scoring logic
- `GameSession.StealYumRoll.cs` — steal attempt and Yum Yum sequencing

## Common Commands

### Prerequisites
- .NET 10 SDK (`dotnet` CLI)

### Running Locally

```bash
dotnet run --project TrashAnimal.Api
```

The API starts with `ASPNETCORE_ENVIRONMENT=Development` by default. OpenAPI spec is available at `/openapi/v1.json`; interactive browser at `/scalar/v1` (dev only).

### Building

```bash
dotnet build
```

Targets all projects in the solution (TrashAnimal, TrashAnimal.Api, TrashAnimal.Tests, TrashAnimal.Api.Tests).

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests for a single project
dotnet test TrashAnimal.Tests
dotnet test TrashAnimal.Api.Tests

# Run a single test file
dotnet test TrashAnimal.Tests --filter "ClassName=GameSessionBustAbandonEndTurnTests"

# Run a single test method (xUnit)
dotnet test TrashAnimal.Tests --filter "FullyQualifiedName~GameSessionBustAbandonEndTurnTests.RollBustAbandonClearsIsPhaseOneActive"
```

### Local Secrets

Credentials required during development are managed by [ASP.NET Core Secret Manager](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets). Secrets are stored outside the repo at:

```
%APPDATA%\Microsoft\UserSecrets\41448837-7d38-4d93-ad0c-2f5aa1557cab\secrets.json
```

The `UserSecretsId` is already configured in `TrashAnimal.Api.csproj`; do **not** run `dotnet user-secrets init`.

**Setting a secret:**
```bash
dotnet user-secrets set "KeyName" "value" --project TrashAnimal.Api
```

**Listing secrets:**
```bash
dotnet user-secrets list --project TrashAnimal.Api
```

**Removing a secret:**
```bash
dotnet user-secrets remove "KeyName" --project TrashAnimal.Api
```

Secrets are loaded only in the `Development` environment. Production will use Azure Key Vault or equivalent.

## Code Patterns & Standards

These standards are enforced via cursor rules (`.cursor/rules/`):

### File Structure
- **Never exceed 500 lines per file.** At 400 lines, split immediately.
- Use folders and naming to keep related files grouped logically.
- Avoid partial classes unless you have a specific reason (e.g., separating large game state machine phases).

### Naming
- All identifiers must be intention-revealing: no `data`, `info`, `temp`, `helper`.
- Use domain language: `ThiefIndex`, `StealTargetZone`, `PhaseOneState`, not generic names.

### Design
- **SOLID principles**: Single Responsibility, Open–Closed, Liskov, Interface Segregation, Dependency Inversion.
- Minimize tight coupling via dependency injection and interfaces.
- Design with unit testing in mind (testable collaborators, narrow interfaces).
- Avoid static methods; prefer instance methods and composition.
- No hardcoded logic; inject strategies and handlers via registries where card/phase rules differ.

### Example: Card Play Handler Pattern

The `RollPhaseGameplayHandlerRegistry` demonstrates the pattern for isolating card-specific rules. When adding a new card:
1. Create a handler implementing the card's eligibility + execution logic.
2. Register it in the handler registry.
3. `GameSession` queries the registry to populate allowed actions; on apply, dispatches to the handler.

This keeps `GameSession` stable and card rules testable in isolation.

## Key Directories

- `TrashAnimal.Api/Application/` — `GameApplicationService`, result DTOs
- `TrashAnimal.Api/Contracts/` — request/response DTOs (Requests, Responses)
- `TrashAnimal.Api/Controllers/` — REST endpoints
- `TrashAnimal.Api/Hubs/` — SignalR hub
- `TrashAnimal.Api/Sessions/` — in-memory session repository
- `TrashAnimal.Api/Startup/` — Options, validators, service registration
- `TrashAnimal.Api/Updates/` — SignalR event/notification types
- `TrashAnimal/RollPhase/` — roll phase handlers, eligibility, card play logic
- `TrashAnimal/TokenPhase/` — token phase coordinator, special token handling
- `TrashAnimal/Scoring/` — end-game scoring
- `TrashAnimal/Helpers/` — utility classes (`Opponents`, etc.)
- `TrashAnimal.Tests/` — domain game logic tests (named `GameSession*Tests.cs`)
- `TrashAnimal.Api.Tests/Contract/` — API contract tests (shape, serialization)
- `TrashAnimal.Api.Tests/Helpers/` — test helpers (`GameApiClient`, `CountingDrawPile`, `SequencedDie`)

## Testing Notes

- Domain tests in `TrashAnimal.Tests/` use xUnit and inject mock/controllable collaborators (e.g., `SequencedDie` for deterministic rolls).
- API tests in `TrashAnimal.Api.Tests/` use `WebApplicationFactory<Program>` for integration testing; `GameApiClient` wraps HTTP calls.
- Contract tests verify enum serialization, response shape, and API surface contracts.
- Tests should not mock the repository; use real in-memory state for integration testing (as per project feedback: mocks can diverge from prod behavior).

## Configuration

Game rules are configurable via `appsettings.json`:
- `GameApplicationServiceOptions.StartingHandCounts` — array of hand sizes per player count (2, 3, 4, 5+ players)

Logging levels are controlled per-environment:
- Development: Debug level for TrashAnimal.Api, Information for Microsoft.AspNetCore
- Production: Information default, Warning for framework
