# Fix: MmmPie-repeated Steal token strands the turn (API sessions)

> **Implementer note:** this plan is written to be executed without further design decisions. Every
> decision is settled below; where a site is listed as "no change," that is a deliberate instruction,
> not an omission. If you believe a listed disposition is wrong, stop and report rather than deviating.
> Line numbers are as of commit on branch `fix/token-phase-steal-exhaustion`; re-locate by symbol name
> if they have shifted, but do not skip a listed site.

## Confirmed diagnosis

**Sequence:** player plays MmmPie (`ResolveTokenTwice = true`), picks Steal, first steal resolves normally (victim declined Doggo, thief picked a card). Completing that pick runs `GameSession.TryCompleteStealWithCard` → `TokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase(stealTokenWasActive: true)` → `TokenPhaseTokenCompletionEngine.TryFinishCurrentTokenPassOrRepeat`, which sees `ResolveTokenTwice` and calls `RestartSubflow(Steal, …)`.

`RestartSubflow`'s Steal branch has exactly two outcomes: no candidates → fizzle (correct, recently fixed), or candidates exist → `TokenPhaseStealHandler.StartViaDelegate`. **`StartViaDelegate` requires `GameSession.ChooseTokenHandStealVictim`, a `Func<>` delegate only ever assigned in `Program.cs` (the CLI harness).** In an API-driven game it is always `null`, so the call fails with `"No token-steal victim selector configured."` and returns `false` **before mutating any state**.

**Resulting stranded state** (matches the reported screenshot exactly):

| Field | Value | Consequence |
|---|---|---|
| `Step` | `ChoosingNextToken` | frontend renders the "RESOLVE A TOKEN" panel |
| `RemainingTokens` | empty | …but with no tokens in it |
| `ActiveToken` | `Steal` | token tray still shows Steal as active |
| `ResolveTokenTwice` | `false` (already consumed) | MmmPie button still offered |
| turn | never ends | `CompleteTokenPhaseAndEndTurn()` unreachable |

**The error is silently swallowed** at `TokenPhaseCoordinator.cs:188` (`out _` discards `error`), so `TryCompleteStealWithCard` returned `true` and the API replied 200 OK on a failed operation. This is why the bug presented as a freeze rather than an error, and is the single biggest reason it survived two prior passes.

**Why only Steal.** A MmmPie repeat is *server-initiated* — no in-flight client request could have pre-supplied the answer. Every other repeat branch either resolves unattended (`DoubleTrash`), or parks in a step the client already renders (`StashTrash`→`StashTrashChooseBranch`, `DoubleStash`→`DoubleStashChoosingCards`, `Recycle`→`RecycleChoosingReplacement`), or is opponent-driven (`Bandit`→`BanditAwaitOpponentResponse`). Steal is the only repeat needing *the active player's* async input, so it is the only one that required a step and never got one.

Three triggers reach the broken branch, all fixed by the same change: completing the first steal's card pick (`CardPickCommand`), the victim blocking with Doggo (`StealPlayDoggo`), and a Kitteh reversal that later completes.

## Key correction to an earlier draft — read before implementing

An earlier version of this plan claimed `GameSession.ApiSupport.TryStartTokenStealWithVictimChoice` needed to branch on `Step` to support the repeat. **That is wrong. Do not add a step branch there.** Verified by reading the method:

- It gates on `State != GameState.TokenPhase` and `playerIndex != CurrentPlayerIndex` only — it never inspects `TokenPhaseStep`.
- It delegates to `TokenPhaseTokenResolver.TryStartHandStealWithVictim`, whose bookkeeping is already idempotent for a repeat: `RemainingTokens.Remove(Steal)` is a no-op when Steal is already gone, and `ActiveToken = Steal` is a no-op when it is already `Steal`.
- It does **not** call `TryStartToken` (which *does* require `Step == ChoosingNextToken`), so that guard is not in the path.

**Therefore the entire server-side command path for the repeat already works.** The only missing piece is that nothing puts the session into a state that tells the client to ask for a victim. This makes the fix substantially smaller than the earlier draft implied.

## Changes

### C1 — New enum member
`TrashAnimal/TokenPhase/TokenPhaseStep.cs`: add `StealChoosingVictim` as the **last** member (appending avoids renumbering any persisted/serialized ordinal).

### C2 — Repeat parks in the new step instead of calling the CLI delegate
`TrashAnimal/TokenPhase/Services/TokenPhaseTokenCompletionEngine.cs`, `RestartSubflow`, `case TokenAction.Steal` (~line 97). Replace the `return _steal.StartViaDelegate(out error);` line **only**. Leave the zero-candidate fizzle branch above it exactly as-is.

```csharp
case TokenAction.Steal:
    if (!_steal.HasCandidates())
    {
        // ... existing fizzle branch, unchanged ...
    }

    // Server-initiated decision point: unlike the first pick, no client request is in flight that
    // could have carried a victim, so park in an explicit step and wait to be asked again.
    state.Step = TokenPhaseStep.StealChoosingVictim;
    return true;
```

Do **not** set `state.ActiveToken` here — it is already `TokenAction.Steal` (guaranteed: `TryFinishCurrentTokenPassOrRepeat` only reaches `RestartSubflow` when `state.ActiveToken` was non-null). Do **not** add a log event here — `RestartSubflow` already calls `TokenPhaseTokenLogRecording.RecordTokenResolutionStarted(_session, token)` at its top, before the switch, so the repeat's start is logged once already. Adding another produces a duplicate "picked a Steal token" log line.

### C3 — Offer the action in the new step
`TrashAnimal/TokenPhase/Services/TokenPhaseAllowedActionsProvider.cs`, inside the `switch (state.Step)` (after the `RecycleChoosingReplacement` case):

```csharp
case TokenPhaseStep.StealChoosingVictim:
    actions.Add(GameAction.ResolveTokenSteal);
    break;
```

**No interrupt cards** (settled decision 1) — do not add MmmPie/Shiny/Feesh here, matching `RecycleChoosingReplacement`'s "you owe an answer" posture. Note this case sits below the `playerIndex != _session.CurrentPlayerIndex` early-return, so it is already correctly scoped to the active player.

### C4 — Leave the new step on the way out
`TrashAnimal/TokenPhase/Services/TokenPhaseTokenResolver.cs`, `TryStartHandStealWithVictim` (~line 269). After the existing `state.ActiveToken = TokenAction.Steal;` line, add:

```csharp
state.Step = TokenPhaseStep.ChoosingNextToken;
```

Rationale: once the steal actually begins, `GameSession.State` becomes `AwaitingStealResponse` and the TokenPhase step is no longer the driver; leaving it as `StealChoosingVictim` would make `TokenPhaseView.step` misreport during the steal. `ChoosingNextToken` is the correct neutral value and is what `TryFinishCurrentTokenPassOrRepeat` sets anyway once the steal resolves. This line is harmless on the first-pick path (step is already `ChoosingNextToken` there).

### C5 — Stop swallowing errors (the fix that prevents recurrence)
Exactly **two** sites discard an `error` out-param and must be fixed. The other five `out _` occurrences discard `resolvedWithNoEffectToken` on a nested call whose caller assigns it explicitly afterward — those are correct; **leave them**.

| File:line | Current | Action |
|---|---|---|
| `TokenPhase/TokenPhaseCoordinator.cs:188` | `TryFinishCurrentTokenPassOrRepeat(_state, out _, out var resolvedWithNoEffectToken)` | **FIX** — capture the error |
| `TokenPhase/Services/TokenPhaseBanditHandler.cs:150` | `_ = _tokenCompletion.TryFinishCurrentTokenPassOrRepeat(state, out _, out resolvedWithNoEffectToken)` | **FIX** — capture the error |
| `TokenPhase/Services/TokenPhaseTokenCompletionEngine.cs:90` | `out error, out _` | leave (discards fizzle token, error propagates) |
| `TokenPhase/Services/TokenPhaseTokenCompletionEngine.cs:102` | `out error, out _` | leave (same) |
| `TokenPhase/Services/TokenPhaseTokenResolver.cs:68` | `out error, out _` | leave (same) |
| `TokenPhase/Services/TokenPhaseTokenResolver.cs:102` | `out error, out _` | leave (same) |
| `TokenPhase/Services/TokenPhaseTokenResolver.cs:256` | `out error, out _` | leave (same) |

For `TokenPhaseCoordinator.OnStealResolvedWhileInTokenPhase` (currently returns `TokenAction?`): change to `public bool OnStealResolvedWhileInTokenPhase(bool stealTokenWasActive, out string? error, out TokenAction? resolvedWithNoEffectToken)`. Its two callers are `GameSession.TryCompleteStealWithCard` and `GameSession.TryStealPlayDoggo` (both in `GameSession.StealYumRoll.cs`) — both must propagate a failure as their own `false` + `error` return, so the HTTP layer produces a 422 instead of a 200. For `TokenPhaseBanditHandler.cs:150`, propagate into that method's existing `out string? error`.

### C6 — Block stacking MmmPie (settled decision 2)
`TrashAnimal/TokenPhase/Services/TokenPhaseInterruptCardPlay.cs`:
- `CanPlayMmmPie` (~line 169): add `&& !state.ResolveTokenTwice` to the returned condition.
- `TryPlayMmmPie`: add an early rejection when `state.ResolveTokenTwice` is already `true`, error text `"A token repeat is already pending."`

**Precise rule:** the block is only on stacking *while a repeat is pending*. Once a repeat has been consumed (`ResolveTokenTwice` back to `false`), playing MmmPie again on a **different** token from `ChoosingNextToken` remains legal — that is existing intended behavior and must not regress.

### C7 — CLI harness handles the new step
`TrashAnimal/Program.cs`: add a branch alongside the existing step branches (lines 161/171/189/198 follow the pattern `if (tp?.Step == TokenPhaseStep.X)`). On `StealChoosingVictim`, prompt the active player's `IPlayerController` for a victim among opponents with non-empty hands, then call `session.TryStartTokenStealWithVictimChoice(currentPlayerIndex, victimIndex, out var error, out _)`.

**Firm directive** (this replaces an earlier "use your judgment" note): after C7, delete `TokenPhaseStealHandler.StartViaDelegate` and the now-unused `GameSession.ChooseTokenHandStealVictim` property, and remove its assignment in `Program.cs` (~line 51). The CLI now drives the same explicit-choice method the API does. Do **not** delete `ChooseShinyStealVictim` or `OnFeeshCardSelection` — those are still used by `RollPhase/ShinyPlayHandler`, `RollPhase/FeeshPlayHandler`, and `TokenPhaseInterruptCardPlay`, and are out of scope.

### C8 — Frontend
- `TrashAnimal.Web/src/api/types.ts:43` — add `'StealChoosingVictim'` to the `TOKEN_PHASE_STEP_VALUES` const array. **Mandatory** per the enum-contract pattern in `TrashAnimal.Web/CLAUDE.md`; the type derives from the const, never hand-write a union.
- `TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx` — add a branch after the `RecycleChoosingReplacement` block:

```tsx
{tokenPhase.step === 'StealChoosingVictim' && (
  <>
    <p …>STEAL AGAIN — CHOOSE A PLAYER</p>
    <button type="button" disabled={isPending} onClick={onStartSteal} …>Choose a player</button>
  </>
)}
```

Reuse the existing `onStartSteal` prop unchanged — it already opens `VictimPicker` filtered to opponents with cards and falls back to `victimSeat: null` when none have any. Match the existing label/button styling conventions in that file (`var(--gb-text-label)` for the heading, `var(--gb-gold)`/`var(--gb-gold-text)` for the button). No changes to `GameBoardPage.tsx` are required.

## Site disposition table — every `TokenPhaseStep` reference

41 references exist across 11 files. Every one is accounted for. **Verify each before finishing** and report any whose actual disposition differs from this table.

| File | Sites | Disposition |
|---|---|---|
| `TokenPhase/TokenPhaseStep.cs` | enum decl | **C1** — add member |
| `TokenPhase/Services/TokenPhaseTokenCompletionEngine.cs` | 44, 75, 79, 110 | **C2** at the Steal case; 44/75/79/110 unchanged |
| `TokenPhase/Services/TokenPhaseAllowedActionsProvider.cs` | 16, 32, 45, 56, 65, 75 | **C3** adds a case; existing cases unchanged |
| `TokenPhase/Services/TokenPhaseTokenResolver.cs` | 44, 87, 91, 110, 123, 140, 146, 154, 187, 285 | **C4** adds one line in `TryStartHandStealWithVictim`. Line 44 (`!= ChoosingNextToken` in `TryStartToken`) **unchanged** — the repeat does not route through `TryStartToken` |
| `TokenPhase/TokenPhaseCoordinator.cs` | 68, 81, 94, 129, 142 | no change — all are `BanditAwaitOpponentResponse`/`StashTrashPickCard`/`DoubleStashChoosingCards`/`RecycleChoosingReplacement` guards, unaffected by a new step |
| `TokenPhase/Services/TokenPhaseBanditHandler.cs` | 23, 34, 54, 135 | no change (all Bandit-step guards) |
| `TokenPhase/Services/TokenPhaseGameActionDispatcher.cs` | 23 | no change — Bandit guard. `ResolveTokenSteal` from the new step flows to `TryStartToken`… **but it must not.** See "Dispatcher caveat" below |
| `TokenPhase/Services/TokenPhaseViewBuilder.cs` | 18, 58, 73 | no change — line 73's allowlist deliberately **excludes** `StealChoosingVictim` (no stashable-card prompt during victim choice); line 18 is the null-state default |
| `TokenPhase/TokenPhaseState.cs` | 19 | no change — default remains `ChoosingNextToken` |
| `TrashAnimal.Api/Application/GameApplicationService.cs` | 268, 273 | no change — that switch handles `CardPickCommand` only; the new step never accepts a card pick |
| `TrashAnimal.Api/Contracts/Requests/GameCommandRequest.cs` | 51 | doc comment only |
| `TrashAnimal/Program.cs` | 161, 171, 189, 198 | **C7** adds a branch |

### Dispatcher caveat (important)
`GameAction.ResolveTokenSteal` submitted as a plain `PlayActionCommand` routes through `TokenPhaseGameActionDispatcher.TryApplyGameAction` → `_tokenResolver.TryStartToken(TokenAction.Steal, …)`, which requires `Step == ChoosingNextToken` **and** `RemainingTokens.Contains(Steal)` — both false during a repeat, so it fails with `"Pick a token only when choosing the next token."` That is acceptable and correct: the client is expected to send `ResolveTokenStealCommand` (which carries the victim), not a bare action. C3 offers `ResolveTokenSteal` in `allowedActions` purely so the **frontend knows to render the picker** — the frontend's `TokenPhasePanel` maps `Steal` to `onStartSteal()` rather than a plain action dispatch (`RESOLVE_TOKEN_ACTION.Steal` is `null` by design). Do not "fix" the dispatcher to accept it.

## Tests

Name tests exactly as given so review can match them to this plan.

**`TrashAnimal.Tests/TokenPhaseStealRepeatTests.cs` (new file)**
1. `MmmPieRepeatOfSteal_AfterFirstStealCompletes_ParksInStealChoosingVictimStep` — assert `Step == StealChoosingVictim`, `ActiveToken == Steal`, `State == GameState.TokenPhase`, turn not ended, `TryCompleteStealWithCard` returned `true` with null error.
2. `MmmPieRepeatOfSteal_AfterDoggoBlock_ParksInStealChoosingVictimStep` — same assertions, triggered via `TryStealPlayDoggo`.
3. `MmmPieRepeatOfSteal_AfterKittehReversalThenCompletion_ParksInStealChoosingVictimStep` — same assertions via the Kitteh path.
4. `StealChoosingVictim_AllowsOnlyResolveTokenSteal` — assert `GetAllowedActionsForPlayer(current)` is exactly `[ResolveTokenSteal]` (proves decision 1: no interrupts), and is empty for non-active players.
5. `StealChoosingVictim_AcceptingVictim_ReopensStealAgainstThatVictim` — submit `TryStartTokenStealWithVictimChoice`, assert `State == AwaitingStealResponse`, `StealVictimIndex` is the chosen one, and the victim is offered `StealPlayDoggo` (the scenario the reporter could not reach).
6. `MmmPieRepeatOfSteal_CanTargetADifferentVictimThanTheFirstSteal` — 3 players; first steal targets seat 1, repeat targets seat 2; assert success.
7. `MmmPieRepeatOfSteal_WithNoRemainingCandidates_StillFizzles` — regression for the existing fizzle path; assert `TokenResolvedWithNoEffectEvent` recorded and turn completes.
8. `MmmPie_CannotBeStackedWhileRepeatPending` — assert `PlayMmmPieTokenPhase` absent from allowed actions and `TryPlayMmmPie` rejects while `ResolveTokenTwice` is true (decision 2).
9. `MmmPie_RemainsPlayableOnADifferentTokenAfterRepeatConsumed` — guards against C6 over-blocking.

**`TrashAnimal.Api.Tests/Integration/TokenPhaseStealRepeatTests.cs` (new file)**
10. `MmmPieRepeatOfSteal_OverHttp_SurfacesStealChoosingVictimAndReopensSteal` — full HTTP walkthrough: MmmPie → `ResolveTokenStealCommand` → victim passes → `CardPickCommand`; assert that response's `view.tokenPhase.step == "StealChoosingVictim"` and `allowedActions` contains `ResolveTokenSteal`; then submit the second `ResolveTokenStealCommand` and assert `view.state == "AwaitingStealResponse"`.

**Frontend**
11. `TokenPhasePanel.test.tsx` — renders the repeat prompt and calls `onStartSteal` when `step === 'StealChoosingVictim'`; renders nothing for it on other steps.

**Existing tests:** all currently-passing tests must still pass unchanged. If C5's signature change breaks call sites in test files, update the call sites mechanically (`out _`) — do not weaken assertions.

## Verification

1. `dotnet build` (solution) — must be 0 errors.
2. `dotnet test TrashAnimal.Tests` and `dotnet test TrashAnimal.Api.Tests` — full runs, not filtered. Baseline before this work: 102 and 74 passing.
3. `cd TrashAnimal.Web && npm run test:run && npm run build`. Baseline: 142 passing.
4. Confirm the site disposition table: report any site whose disposition differed from the table.
5. **Manual, required before declaring done** — drive a 3-player game (`dotnet run --project TrashAnimal.Api` + `npm run dev`): MmmPie → Steal → victim declines Doggo → thief picks. Confirm the victim picker reappears, a *different* opponent can be targeted, and that opponent is independently offered Doggo. Automated tests written alongside a fix are the least trustworthy evidence for exactly this interaction — it is the one that has now failed three times.

## Decisions (settled 2026-07-29 — do not revisit)

1. **No interrupt cards during `StealChoosingVictim`.** `ResolveTokenSteal` only.
2. **MmmPie cannot be stacked while a repeat is pending** (C6), but stays playable on a different token after a repeat is consumed.
3. **Do not unify the first-pick flow onto the new step.** The asymmetry (client *predicts* the victim question on first pick from `remainingTokens`; server *declares* it on repeat via `Step`) is accepted. Both paths already converge on `TokenPhaseStealHandler.StartWithVictim`, so there is one implementation of "begin the hand-steal." Unification is a follow-up ticket, not part of this work.

## Out of scope

- Unifying the RollPhase vs TokenPhase Shiny/Feesh duplication (existing `// todo refactor this`).
- `ChooseShinyStealVictim` / `OnFeeshCardSelection` delegates — still legitimately used; only `ChooseTokenHandStealVictim` is removed (C7).
- Any change to `GameSession.ApiSupport.TryStartTokenStealWithVictimChoice` — see "Key correction" above.
