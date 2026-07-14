# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in `TrashAnimal`, the domain/game-engine project. See the [repo-root CLAUDE.md](../CLAUDE.md) for shared commands, standards, and cross-project context.

## Project Overview

**TrashAnimal** is the game engine — turn structure, phase states, card effects, scoring — with zero external NuGet dependencies and no framework/DI container. It targets `net10.0`, builds as an **Exe** (not a pure class library): `Program.cs` is a console CLI harness used to manually play/test the game outside the API. `TrashAnimal.Api` references this project and drives the same engine over HTTP/SignalR.

## Folder Structure

- **`RollPhase/`** — "Phase 1" (rolling) card-play strategy objects. `IGameplayHandler` (Action, IsActionable, TryExecute) is the strategy interface; `RollPhaseGameplayHandlerRegistry` looks handlers up by `GameAction`; `RollPhaseGameplayHandlers.CreateDefault()` wires up `ShinyPlayHandler`, `FeeshPlayHandler`, `NannersBustRecoveryHandler`, `BlammoBustRecoveryHandler`. `RollPhaseOfferSnapshot` (read-only) and `RollPhasePlayContext` (mutation-capable) are the two context objects passed to handlers.
- **`TokenPhase/`** — "Phase 2" (resolving rolled tokens) sub-state-machine. `TokenPhaseCoordinator` is owned 1:1 by `GameSession` and orchestrates `TokenPhaseState`/`TokenPhaseStep`. `Services/` holds the per-concern collaborators: `TokenPhaseTokenResolver` (core resolution logic per token type), `TokenPhaseBanditHandler` (Bandit reveal/response flow), `TokenPhaseInterruptCardPlay` (MmmPie/Shiny/Feesh interrupts during TokenPhase), `TokenPhaseAllowedActionsProvider`, `TokenPhaseGameActionDispatcher`, `TokenPhaseViewBuilder`.
- **`Scoring/`** — `ScoreManager` orchestrates end-game scoring; `CardEndGameScoringCatalog` defines ranked cards and tier point values; `TieredCardScoringCalculator` handles "most of a card wins tiered points" with tie-splitting.
- **`Helpers/`** — small static helpers, e.g. `Opponents` (GetAllWithNonEmptyHand / GetAllWithNonEmptyStash).
- **Root files** — core session/state machine, card/data model, action enums, steal mechanic, Yum Yum window, views/DTOs, and the CLI harness (see below).

## Key Types

### GameSession (`GameSession.cs` + 3 partials)
The orchestrator. Holds `IDrawPile`, players, the RollPhase handler registry, `YumYumWindow`, `TokenPhaseCoordinator`, `StealAttempt`, `PhaseOneState`, discard pile, and `GameState`.
- `GameSession.cs` — constructor (validates 2–4 players), `GetAllowedActionsForPlayer`, `ApplyAction`, `BeginTurn`/`EndTurn`, `GetViewForPlayer`.
- `GameSession.ApiSupport.cs` — explicit-choice overloads (`TryPlayFeeshWithCardChoice`, `TryPlayShinyWithVictimChoice`, `TryStartTokenStealWithVictimChoice`) that bypass the `Func<>` delegate pattern — this is the surface `TrashAnimal.Api` calls into instead of the CLI's delegate-callback style.
- `GameSession.GameEnd.cs` — deck-exhaustion detection, `FinalizeGameEnd`, scoring via `ScoreManager`.
- `GameSession.StealYumRoll.cs` — RollPhase handler dispatch, steal-response methods, Yum Yum response, `RollDie`.

Extensibility points for player decisions: `OnFeeshCardSelection`, `ChooseShinyStealVictim`, `ChooseTokenHandStealVictim` (`Func<>` delegates). `GameSession` does **not** know about `IPlayerController` — `Program.cs` (the CLI) is the glue that adapts `IPlayerController` calls into those delegates. The API project instead uses `GameSession.ApiSupport.cs`'s explicit-choice methods, since an HTTP request already carries the choice.

### Card model
`Card` (Id + `CardName`), `CardName` enum (Blammo, Nanners, Feesh, Shiny, Yumyum, MmmPie, Kitteh, Doggo), `Deck : IDrawPile` (hard-coded 50-card composition, Fisher–Yates shuffle), `Hand`/`HandEntry` (tracks `NewlyAdded` to stop cards drawn mid-TokenPhase-pass from being played immediately), `StashPile`/`StashEntry` (tracks `IsFaceUp`), `Player` (Index, Name, Hand, StashPile), `Die` (thin `Random` wrapper; `Roll()` is `virtual` for test overriding — there is **no `IDie` interface**, only subclassing).

### Steal mechanic
`StealAttempt` owns thief/victim/zone state independent of `GameSession`: `Begin`, `TryRefuseToBlockSteal`, `TryPlayDoggo` (victim draws 2, blocks steal), `TryPlayKitteh` (swaps thief/victim roles), `TryCompletePick`. Supporting types: `StealAttemptAftermath`, `StealTargetZone`, `StealPickSlot`/`StealPickSlotBuilder` (stash slots reveal name only if face-up; hand slots are always unrevealed), `StealPhaseView` (UI projection).

### Yum Yum window
`YumYumWindow` — clockwise opponent-response queue after a voluntary stop, independent of `GameSession`. Playing Yum Yum discards it and forces another roll. Has a `// todo` noting it should become an async multi-responder race window (currently sequential).

### Views / DTOs
`GameView` (top-level per-player snapshot from `GetViewForPlayer` — hides other players' hand contents), `TokenPhaseView` (nested TokenPhase UI state), `GameEndScoreLine` (one scoreboard row).

## Interfaces / DI Seams

- **`IDrawPile`** — `GetDeckCount()`, `DealCards(count)`. Implemented by `Deck`; injected via `GameSession` constructor, enabling a scripted/fake deck in tests.
- **`IPhaseTwo`** — placeholder for TokenPhase resolution; only implementation is `PhaseTwoNoop`. **Not used** by `GameSession` anymore — TokenPhase resolution is handled directly by `TokenPhaseCoordinator`. Treat as leftover scaffolding from an earlier design, not a pattern to extend.
- **`IPlayerController`** — the human/AI seam used only by the CLI harness (`CliHumanController`, `AiController`). `GameSession` never references it directly.
- **`IGameplayHandler`** — RollPhase card-play strategy interface (see above).
- **`ITokenPhaseTokenCompletion`** — narrow internal seam so `TokenPhaseBanditHandler` can trigger "finish current token" without depending on `TokenPhaseCoordinator` directly.

No DI container is used anywhere in this project — composition is entirely manual (constructor parameters).

## Game Flow (State Machine)

```
RollPhase ⇄ AwaitingYumYum (voluntary stop, opponents respond clockwise)
RollPhase → AwaitingStealResponse → AwaitingStealCardPick → resume to RollPhase or TokenPhase
RollPhase → TokenPhase → TurnEnd → BeginTurn (next player) → RollPhase   [or GameEnded]
```

- **RollPhase**: roll for a unique `TokenAction` face (bust on repeat), or play Shiny/Feesh while not busted. While busted: only Nanners (clear bust, stop) / Blammo (clear bust, forced reroll) / AbandonBust (draw 1, skip straight to TurnEnd).
- **Voluntary stop → Yum Yum window** → back to RollPhase; `AdvanceToResolveTokens` (offered once stopped/busted-cleared with no forced roll pending) moves into TokenPhase, or straight to TurnEnd if no tokens were collected.
- **TokenPhase**: resolve each collected `TokenAction` (StashTrash, DoubleStash, DoubleTrash, Bandit, Steal, Recycle) one at a time via `TokenPhaseCoordinator`; MmmPie re-runs the just-finished token once more. Steal token triggers the same `AwaitingStealResponse` flow as Shiny, but on the Hand zone instead of Stash.
- **Steal sub-flow**: victim may pass (thief picks a card), play Doggo (fully blocks, victim draws 2), or play Kitteh (swaps thief/victim roles). Resumes into whichever phase (`RollPhase`/`TokenPhase`) the steal originated from.
- **TurnEnd**: only `EndTurn` is legal. If the deck was emptied during this turn, ends the game (`FinalizeGameEnd`) instead of advancing to the next player.
- **GameEnded**: terminal; `GetGameEndScoreSummary()`/`GetGameEndResult()` become available.

Full action vocabulary: `GameAction.cs`. Token faces: `TokenAction.cs` (StashTrash, DoubleStash, DoubleTrash, Bandit, Steal, Recycle).

## CLI Harness

`Program.cs`, `Cli.cs`, `CliHumanController`, `AiController`, `PhaseTwoNoop`, `EnumExtensions` are a manual-play console driver for exercising the engine directly — not part of the API's request path. Useful for quickly trying out a rules change without going through `TrashAnimal.Api`:

```bash
dotnet run --project TrashAnimal
```

## Other Files Worth Knowing About

- **`Notes.txt`** (repo root) — a self-authored refactor memo proposing the strategy/registry pattern for card handlers. It reads as **historical design rationale that has since been substantially implemented** (`IGameplayHandler` + registry + per-card handler classes, `StealAttempt`, `YumYumWindow` all exist as described) rather than an outstanding TODO list.
- **`scenarios.txt`** — hand-written gameplay scenarios walking through Shiny/Feesh/Nanners/Blammo interplay with expected "Options should be [...]" outputs. Informal acceptance-test documentation, not an xUnit file — actual tests live in `TrashAnimal.Tests`.
- Scattered `// todo` comments flag known rough edges: `YumYumWindow` (should become an async race window, currently sequential), `TokenPhaseInterruptCardPlay` (overlap with RollPhase card logic worth refactoring).
