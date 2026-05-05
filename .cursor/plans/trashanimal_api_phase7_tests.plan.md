---
name: trashanimal_api_phase7_tests
overview: Define the test strategy for TrashAnimal.Api covering API integration tests, contract tests, and the relationship to the existing TrashAnimal engine test suite.
todos:
  - id: api-integration-tests
    content: Add API integration tests for turn transitions, allowed/disallowed actions, token phase, and end-game
    status: pending
  - id: contract-tests
    content: Add contract tests for enum string serialization, GameView DTO shape, and hidden-info boundaries
    status: pending
  - id: engine-baseline
    content: Confirm existing TrashAnimal.Tests still pass without modification after all API phases are complete
    status: pending
isProject: false
---

# Phase 7: Test Strategy

## Goal

Ensure the API layer is correct, stable, and that the engine behavior is not regressed during migration.

## Test Projects

- `TrashAnimal.Tests` — existing engine tests; must pass unchanged after all phases. Serves as the behavioral baseline.
- `TrashAnimal.Api.Tests` — new project using `WebApplicationFactory<Program>` (or equivalent) for in-process API integration tests.

## API Integration Tests

Add tests in `TrashAnimal.Api.Tests` covering the following scenarios end-to-end (HTTP in, HTTP out):

- **Allowed/disallowed actions** — submitting an action not in `GetAllowedActionsForPlayer` returns a 4xx with an error message.
- **Full turn transition** — roll phase → token phase → end turn advances `GameState` correctly.
- **Token phase interrupt paths** — interrupt card plays are accepted at the correct moment and rejected otherwise.
- **End-game scoring** — game completes to `GameEnded` and `GET /games/{gameId}/result` returns a correct `GameEndResult` via [`TrashAnimal/GameSession.GameEnd.cs`](c:/Users/Seth/Source/Repos/TrashAnimal/TrashAnimal/GameSession.GameEnd.cs).
- **Concurrency guard** — two simultaneous requests on the same `gameId` do not corrupt state.

## Contract Tests

Add serialization/contract tests asserting:

- All enum fields (`GameAction`, `GameState`, `TokenAction`, `CardName`) in request and response bodies serialize as strings, never integers.
- `GameView` response DTO shape matches the engine record structure (no missing or extra fields that would silently break the frontend).
- Opponent hand contents are absent from per-player view responses (hidden-information boundary).

## Regression Check

- Run `TrashAnimal.Tests` as part of the same CI pipeline after every phase.
- If any engine behavior changes in Phase 4 (CLI decouple), update the affected test setup in `TrashAnimal.Tests` in the same commit.
