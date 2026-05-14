---
name: trashanimal_api_phase4_cli_decouple
overview: Remove all CLI-coupled decision paths from GameSession so every interaction can be driven by HTTP commands with no Console dependency.
todos:
  - id: audit-callbacks
    content: Audit and replace delegate-based choice hooks (OnFeeshCardSelection, ChooseShinyStealVictim, ChooseTokenHandStealVictim) with explicit command endpoints or inline payloads
    status: completed
  - id: remove-console-coupling
    content: Ensure no Console I/O leaks into TrashAnimal.Api execution paths
    status: completed
  - id: verify-all-branches-reachable
    content: Confirm every playable branch is triggerable via ApplyAction / Try* without CLI involvement
    status: completed
isProject: false
completed: true
completedAt: 2026-05-07
---

# Phase 4: Replace CLI Interaction Paths

## Goal

Ensure every decision branch in the game engine can be driven entirely by HTTP commands. Eliminate any path where `TrashAnimal.Api` would need `Console` input or synchronous in-process callbacks.

## Tasks

### 4.1 Audit Delegate-Based Choice Hooks

The following callbacks are currently wired in [`TrashAnimal/Program.cs`](c:/Users/Seth/Source/Repos/TrashAnimal/TrashAnimal/Program.cs) via synchronous delegates on `GameSession`:

- `OnFeeshCardSelection` — player selects a card when a Feesh is played.
- `ChooseShinyStealVictim` — player chooses who to steal from (Shiny card).
- `ChooseTokenHandStealVictim` — player chooses token hand steal target.

Replace each with one of:

- **Inline payload** — include the choice in the original command request body where the rules allow it to be submitted simultaneously.
- **Pending-choice state** — engine moves to a new `GameState` awaiting the choice; a follow-up `POST /games/{gameId}/commands` with the choice payload completes the transition.

Prefer the pending-choice state model for consistency with how steal/YumYum phases already work.

### 4.2 Remove Console Coupling

- Audit `TrashAnimal.Api` for any transitive dependency on `Console.*` calls.
- `CliHumanController` and `Cli` types must not be referenced by `TrashAnimal.Api`.
- `TrashAnimal/Program.cs` (CLI host) may remain in the `TrashAnimal` project for local developer testing but must not be referenced or depended on by any `TrashAnimal.Api` code path.

### 4.3 Verify All Branches Are API-Reachable

- Walk through each `GameState` and confirm `GetAllowedActionsForPlayer` + the corresponding `ApplyAction`/`Try*` path can be exercised without CLI involvement.
- Document any branch that required a new command endpoint or payload field as part of this phase.

## Constraints

- Changes to `GameSession` delegate hooks should be additive where possible — avoid breaking existing engine behavior relied upon by `TrashAnimal.Tests`.
- If a hook must change signature, update the corresponding test setup at the same time.
