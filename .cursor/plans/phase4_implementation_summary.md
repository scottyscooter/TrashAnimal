# Phase 4 Implementation Summary

## Objective
Remove all CLI-coupled decision paths from GameSession so every interaction can be driven by HTTP commands with no Console dependency.

## Changes Made

### 1. New API-Friendly Methods (GameSession.ApiSupport.cs)

Created three new methods that accept player choices as parameters instead of using delegates:

- **`TryPlayFeeshWithCardChoice(int playerIndex, Guid discardCardId, out string? error)`**
  - Plays a Feesh card to retrieve a specific card from the discard pile
  - Bypasses the `OnFeeshCardSelection` delegate
  - Used by API when handling `PlayFeesh` action with `CardId` payload

- **`TryPlayShinyWithVictimChoice(int playerIndex, int victimIndex, out string? error)`**
  - Plays a Shiny card to steal from a specific opponent's stash
  - Bypasses the `ChooseShinyStealVictim` delegate
  - Used by API when handling `PlayShiny` action with `VictimSeat` payload

- **`TryStartTokenStealWithVictimChoice(int playerIndex, int victimIndex, out string? error)`**
  - Starts the Steal token resolution by selecting a victim
  - Bypasses the `ChooseTokenHandStealVictim` delegate
  - Used by API when handling `ResolveTokenSteal` action with `VictimSeat` payload

### 2. API Service Updates (GameApplicationService.cs)

Updated `ExecuteCommandUnlockedAsync` to intercept and handle the three special actions:

- **`GameAction.PlayFeesh`** - Requires `CardId`, calls `TryPlayFeeshWithCardChoice`
- **`GameAction.PlayShiny`** - Requires `VictimSeat`, calls `TryPlayShinyWithVictimChoice`
- **`GameAction.ResolveTokenSteal`** - Requires `VictimSeat`, calls `TryStartTokenStealWithVictimChoice`

These actions are now handled BEFORE falling through to the generic `ApplyAction` call, ensuring the delegates are never invoked from the API layer.

### 3. Request Documentation (SubmitCommandRequest.cs)

Updated the documentation to reflect the new payload requirements:
- `PlayFeesh` requires `CardId` (card to retrieve from discard pile)
- `PlayShiny` requires `VictimSeat` (opponent to steal from)
- `ResolveTokenSteal` requires `VictimSeat` (opponent to steal from)

### 4. Snapshot Updates (GameSession.StealYumRoll.cs)

Modified `CreateRollPhaseOfferSnapshot` to always return `true` for:
- `HasFeeshSelector`
- `HasShinyVictimSelector`

This ensures that `PlayFeesh` and `PlayShiny` actions are always offered in the allowed actions list, regardless of whether delegates are set. The handlers will still fail gracefully with clear error messages if the delegates are missing when called via `ApplyAction`.

## Delegate Handling Strategy

The three delegates are now **optional** for API use:
- **CLI**: Sets up delegates in `Program.cs` (lines 39-61), uses `ApplyAction` with handlers that call delegates
- **API**: Does NOT set up delegates, intercepts special actions and uses new `Try*` methods directly

The handlers (`FeeshPlayHandler`, `ShinyPlayHandler`, `TokenPhaseTokenResolver.StartHandSteal`) still check for delegates and fail gracefully if they're null. This provides backward compatibility with the CLI while enabling delegate-free API operation.

## Console Coupling Verification

Confirmed that `TrashAnimal.Api` project has:
- **No `Console.*` references**
- **No `CliHumanController` or `Cli` class references**
- **No transitive dependencies on CLI infrastructure**

The CLI code remains in `TrashAnimal/Program.cs` for local developer testing but is completely decoupled from the API execution paths.

## API Completeness

All game branches are now reachable via HTTP commands:
- ✅ Roll phase actions (RollDie, StopRolling, AdvanceToResolveTokens)
- ✅ Card plays (PlayFeesh with CardId, PlayShiny with VictimSeat, PlayNanners, PlayBlammo)
- ✅ Bust handling (AbandonBust)
- ✅ YumYum responses (YumYumPlay, YumYumPass)
- ✅ Steal responses (StealPass, StealPlayDoggo, StealPlayKitteh)
- ✅ Steal card selection (AwaitingStealCardPick state with CardId)
- ✅ Token phase actions (all token types including ResolveTokenSteal with VictimSeat)
- ✅ Token phase substeps (Bandit, StashTrash, DoubleStash, Recycle)
- ✅ Turn lifecycle (EndTurn)

## Test Results

All 60 existing tests pass without modification, confirming backward compatibility with CLI usage patterns.
