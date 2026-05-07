---
name: trashanimal_api_phase2_command_contracts
overview: Define all API request/response DTOs in TrashAnimal.Api and establish the mapping to TrashAnimal engine types.
todos:
  - id: define-dtos
    content: Define request/response DTOs for create game, get view, submit action, and choice payloads
    status: completed
  - id: map-enums
    content: Map API-layer enums to engine enums (GameAction, GameState, TokenAction, CardName)
    status: completed
  - id: verify-string-enums
    content: Verify all API enums serialize as strings end-to-end
    status: completed
isProject: false
---

# Phase 2: Standardize Command Contracts

## Goal

Establish a stable API contract surface in `TrashAnimal.Api` that is decoupled from engine internals and versioning-safe for the frontend.

## Tasks

### 2.1 Define Request/Response DTOs

Create DTOs inside `TrashAnimal.Api` (not in the `TrashAnimal` engine project) for:

- **Create game** — player names/count, optional seed for `Die`/`Deck` randomness.
- **Get current view** — response wraps per-player `GameView` from the engine, including current `GameState` and allowed `GameAction` list.
- **Submit action** — `gameId`, `playerSeat`, `GameAction` discriminator, plus optional payload fields (see 2.2).
- **Choice payloads** — inline on the submit action DTO or as separate follow-up DTOs:
  - card id(s) (`Guid`) for steal card pick, recycle pick, stash-trash pick.
  - victim seat index for steal victim selection, token hand steal victim.
  - selected `TokenAction` for double stash or bandit.

### 2.2 Map API Enums to Engine Enums

- Engine enums used in the API surface: [`TrashAnimal/GameAction.cs`](c:/Users/Seth/Source/Repos/TrashAnimal/TrashAnimal/GameAction.cs), [`TrashAnimal/GameState.cs`](c:/Users/Seth/Source/Repos/TrashAnimal/TrashAnimal/GameState.cs), [`TrashAnimal/TokenAction.cs`](c:/Users/Seth/Source/Repos/TrashAnimal/TrashAnimal/TokenAction.cs), `CardName`.
- Engine enums may be exposed directly in DTOs (no parallel API enum) or re-declared in `TrashAnimal.Api` for versioning isolation — choose one approach and apply it consistently.
- All enums must serialize as strings (enforced by global `JsonStringEnumConverter` from Phase 1).

### 2.3 Verify String Enum Serialization

- Confirm all enum fields in every DTO round-trip as strings in requests and responses.
- Add a unit test or serialization smoke test asserting enum string form before writing further integration tests.

## Constraints

- Engine internals (`Card`, `Hand`, `StashPile`, `Deck`) must not appear in API DTOs; always project through engine view types (`GameView`, `TokenPhaseView`, `StealPhaseView`).
- Hidden-information boundaries (opponent hand contents) must be absent from per-player view DTOs.
