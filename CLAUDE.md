# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working anywhere in this repository. It covers what's shared/reusable across the whole solution — overview, cross-cutting architecture, build/test/run commands, and coding standards. **Project-specific detail lives in each project's own CLAUDE.md** — read the relevant one(s) before working in that project:

- [TrashAnimal/CLAUDE.md](TrashAnimal/CLAUDE.md) — domain/game-engine (GameSession, RollPhase, TokenPhase, Scoring, CLI harness)
- [TrashAnimal.Api/CLAUDE.md](TrashAnimal.Api/CLAUDE.md) — REST API + SignalR hub (endpoints, DTOs, CORS status, hosting config)
- [TrashAnimal.Web/CLAUDE.md](TrashAnimal.Web/CLAUDE.md) — frontend (currently a barebones Vite + React + TypeScript scaffold; no architecture decisions made yet)

There is no CLAUDE.md yet for `TrashAnimal.Tests` or `TrashAnimal.Api.Tests` — see "Testing Notes" below for what's known about them; add project-specific files there if/when they grow enough to warrant it.

## Project Overview

**TrashAnimal** is a digital implementation of a card game. The solution consists of:

- **TrashAnimal** — core domain logic (game rules, turn structure, card effects, scoring); also builds as a console CLI for manual play/testing. .NET 10, zero external dependencies.
- **TrashAnimal.Api** — ASP.NET Core 10 REST API + SignalR hub for multiplayer game sessions. In-memory only, no persistence yet.
- **TrashAnimal.Web** — browser client. Vite + React 19 + TypeScript. Currently scaffold-only; not wired to the API yet.
- **TrashAnimal.Tests** — xUnit tests for domain game logic.
- **TrashAnimal.Api.Tests** — xUnit tests for API contracts and integration scenarios.

`TrashAnimal.slnx` is the dotnet solution file; it currently lists `TrashAnimal`, `TrashAnimal.Api`, and `TrashAnimal.Tests` (not `TrashAnimal.Api.Tests`, and not `TrashAnimal.Web` — the latter is a plain npm project, not a .NET project, and builds/runs independently via `npm`).

The .NET projects target .NET 10 with nullable reference types enabled (`<Nullable>enable</Nullable>`).

## Cross-Cutting Architecture

**Key Architectural Boundaries** (full detail in each project's CLAUDE.md):
- Game state flows through `GameSession.GetAllowedActionsForPlayer()` → controller/hub receives validated actions → caller submits a command → `GameApplicationService.DispatchCommandAsync()` calls into `GameSession` → domain publishes a state-change event.
- SignalR is **notification-only**; all game commands go through REST (`POST /games/{id}/commands`). The hub only tells clients "something changed, go re-fetch" — never carries command payloads.
- Hidden information: each player sees only their own hand via `PlayerViewResponse`/`GameView`; opponent card counts are visible but not card identities.
- Enums serialize as strings across all JSON (`JsonStringEnumConverter` wired for controllers, minimal APIs, and the SignalR JSON protocol in `TrashAnimal.Api/Program.cs`).
- `TrashAnimal.Web` will eventually consume the REST + SignalR surface described in [TrashAnimal.Api/CLAUDE.md](TrashAnimal.Api/CLAUDE.md); the API's `"Frontend"` CORS policy (configured via `CorsOptions:AllowedOrigins`, default `http://localhost:5173`) allows this.

## Common Commands

### Prerequisites
- .NET 10 SDK (`dotnet` CLI)
- Node.js + npm (for `TrashAnimal.Web` — see [TrashAnimal.Web/CLAUDE.md](TrashAnimal.Web/CLAUDE.md) for frontend-specific commands)

### Running Locally

```bash
dotnet run --project TrashAnimal.Api
```

The API starts with `ASPNETCORE_ENVIRONMENT=Development` by default. OpenAPI spec is available at `/openapi/v1.json`; interactive browser at `/scalar/v1` (dev only).

### Building

```bash
dotnet build
```

Targets the projects listed in `TrashAnimal.slnx`. `TrashAnimal.Web` is not part of this — build it separately (`npm run build` inside that folder).

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

These standards apply solution-wide and are enforced via cursor rules (`.cursor/rules/`):

### File Structure
- **Never exceed 500 lines per file.** At 400 lines, split immediately.
- Use folders and naming to keep related files grouped logically.
- Avoid partial classes unless you have a specific reason (e.g., separating large game state machine phases — `GameSession` is the deliberate exception; see [TrashAnimal/CLAUDE.md](TrashAnimal/CLAUDE.md)).

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

The `RollPhaseGameplayHandlerRegistry` (in `TrashAnimal/RollPhase/`) demonstrates the pattern for isolating card-specific rules. When adding a new card:
1. Create a handler implementing the card's eligibility + execution logic.
2. Register it in the handler registry.
3. `GameSession` queries the registry to populate allowed actions; on apply, dispatches to the handler.

This keeps `GameSession` stable and card rules testable in isolation. See [TrashAnimal/CLAUDE.md](TrashAnimal/CLAUDE.md) for the full type breakdown.

## Testing Notes

- Domain tests in `TrashAnimal.Tests/` use xUnit and inject mock/controllable collaborators (e.g., `SequencedDie` for deterministic rolls).
- API tests in `TrashAnimal.Api.Tests/` use `WebApplicationFactory<Program>` for integration testing; `GameApiClient` wraps HTTP calls.
- Contract tests verify enum serialization, response shape, and API surface contracts.
- Tests should not mock the repository; use real in-memory state for integration testing (as per project feedback: mocks can diverge from prod behavior).
- Test helper locations: `TrashAnimal.Api.Tests/Contract/` (API contract tests), `TrashAnimal.Api.Tests/Helpers/` (`GameApiClient`, `CountingDrawPile`, `SequencedDie`).

## Configuration

Game rules are configurable via `TrashAnimal.Api/appsettings.json`:
- `GameApplicationServiceOptions.StartingHandCounts` — array of hand sizes per player count (2, 3, 4, 5+ players), validated to be 2–4 entries.

Logging levels are controlled per-environment:
- Development: Debug level for TrashAnimal.Api, Information for Microsoft.AspNetCore
- Production: Information default, Warning for framework
