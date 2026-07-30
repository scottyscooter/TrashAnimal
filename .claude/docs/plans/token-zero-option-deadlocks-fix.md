# Fix: TokenPhase steps reachable with zero valid options (Recycle + StashTrash)

> **Implementer note:** this plan is written to be executed without further design decisions. Every
> decision is settled below; where a site is listed as "no change," that is a deliberate instruction,
> not an omission. If you believe a listed disposition is wrong, stop and report rather than deviating.
> Line numbers are as of branch `fix/token-phase-steal-exhaustion`; re-locate by symbol name if they
> have shifted, but do not skip a listed site.

## Summary

Two token types can enter a resolution step that offers the player **zero** legal moves, permanently
stranding the turn. Both are the same bug shape already fixed for Steal (no hand candidates) and Bandit
(empty deck) earlier on this branch — these are the two instances that were missed, because that earlier
pass fixed only the tokens named in the bug report rather than auditing all six.

- **B1 — Recycle with zero replacement options.** Confirmed live (see repro below).
- **B2 — StashTrash stash-mode with zero stashable hand cards.** Confirmed by code reading; not yet observed live.

## Completeness audit — all six token types

This is the artifact that was missing from the earlier pass. Every `TokenAction` was checked for
"can this reach a state where the active player has no legal move?"

| Token | Reachable zero-option state? | Status |
|---|---|---|
| **Recycle** | **Yes** — if all six faces were rolled, `GetRecycleOptions` returns empty, but `TryStartToken` enters `RecycleChoosingReplacement` unconditionally. That step contributes **no** `allowedActions` by design (the client drives it from `recycleReplacementOptions`), so an empty option list means no legal move at all. | **B1 — fix here** |
| **StashTrash** | **Yes** — the stash branch. `TryStashTrashEnterStashMode` moves to `StashTrashPickCard` unconditionally, but `CanOfferCardForStashPrompt` excludes `Doggo`/`Kitteh`, so a hand that is empty or holds only Doggo/Kitteh yields an empty prompt. `StashTrashPickCard`'s only `allowedActions` are interrupt cards (MmmPie/Shiny/Feesh); Doggo/Kitteh are not interrupts, so a hand of exactly `{Doggo, Kitteh}` gives **empty `allowedActions`** and there is no back/cancel action. | **B2 — fix here** |
| DoubleStash | No — `TokenDoubleStashSubmit` is offered unconditionally and submitting an **empty** list is legal (`cardIds.Count > 2` is the only cap), so it always self-resolves. | safe, no change |
| DoubleTrash | No — resolves unattended. `DealCards(2)` on an empty pile returns 0 cards and still finishes the pass. | safe, no change |
| Bandit | Was yes (empty deck). | already fixed on this branch |
| Steal | Was yes (no opponent hand candidates). | already fixed on this branch |

## B1 — Recycle with zero replacement options

**Confirmed reproduction.** Game `d5c5fd3c-b2f8-43ab-89a1-c59145c45f62`, turn 10, from an automated
13-game playthrough. Player rolled all six faces (`StashTrash, Recycle, Steal, DoubleTrash, DoubleStash,
Bandit`), resolved the other five, then picked Recycle last. Resulting stranded state:

```json
{ "step": "RecycleChoosingReplacement", "remainingTokens": [], "activeToken": "Recycle",
  "recycleReplacementOptions": [] }
```

**Mechanism.** `TokenPhaseViewBuilder.GetRecycleOptions` returns every `TokenAction` that is neither
`Recycle` itself nor present in `state.InitialTokensSnapshot`. Rolling is unique-or-bust across six
faces, so rolling all six puts all five non-Recycle types in the snapshot and the option list is empty.
`TokenPhaseTokenResolver.TryStartToken`'s `case TokenAction.Recycle` (line ~109) sets the step with no
options check, and `TokenPhaseTokenCompletionEngine.RestartSubflow`'s `case TokenAction.Recycle`
(line ~112) does the same on an MmmPie repeat.

**Exact reachability:** requires all six faces rolled without busting. Rare per-turn, but it occurred
once in 13 automated games, so it is not a theoretical concern.

### B1 changes

**B1.1 — give the completion engine access to the option list.**
`TokenPhaseTokenCompletionEngine`'s constructor is currently `(GameSession, TokenPhaseBanditHandler,
TokenPhaseStealHandler)` and has no way to ask for Recycle options. Add a `TokenPhaseViewBuilder`
parameter and store it. `TokenPhaseTokenResolver`'s constructor already holds `_viewBuilder` and
constructs the engine, so pass it through there — no other construction site exists and there is no
dependency cycle.

**Firm directive:** do **not** introduce a new `TokenPhaseRecycleHandler` collaborator for this. It
would be more symmetric with `TokenPhaseStealHandler`/`TokenPhaseBanditHandler`, but it requires moving
`GetRecycleOptions` off the view builder (which also uses it) and is a speculative refactor this fix
does not need. Keep the change minimal.

**B1.2 — fizzle in `TryStartToken`** (`TokenPhaseTokenResolver.cs`, `case TokenAction.Recycle`, ~line 109).
Replace the unconditional step assignment with the established fizzle pattern, copied in shape from the
Bandit branch immediately above it:

```csharp
case TokenAction.Recycle:
    if (_viewBuilder.GetRecycleOptions(state).Count == 0)
    {
        state.RemainingTokens.Remove(token);
        state.ActiveToken = null;
        TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
        var recycleFinished = _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
        resolvedWithNoEffectToken = token;
        return recycleFinished;
    }

    state.Step = TokenPhaseStep.RecycleChoosingReplacement;
    return true;
```

Note the `RemainingTokens.Remove(token)` / `ActiveToken = null` lines are required here because, unlike
the other tokens in that switch, the Recycle case sits **after** the shared
`state.RemainingTokens.Remove(token); state.ActiveToken = token;` lines only for non-Steal tokens —
verify against the actual file and match whichever bookkeeping the sibling Bandit fizzle branch performs
at that point, rather than copying these two lines blindly.

**B1.3 — fizzle in `RestartSubflow`** (`TokenPhaseTokenCompletionEngine.cs`, `case TokenAction.Recycle`,
~line 112), mirroring its sibling Bandit/Steal fizzle branches in the same switch:

```csharp
case TokenAction.Recycle:
    if (_viewBuilder.GetRecycleOptions(state).Count == 0)
    {
        state.ActiveToken = null;
        TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
        var finished = TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
        resolvedWithNoEffectToken = token;
        return finished;
    }

    state.Step = TokenPhaseStep.RecycleChoosingReplacement;
    return true;
```

**B1.4 — messages.** Both fizzle-message switches currently fall through to a generic default for
Recycle; give it a specific case in each:
- `TrashAnimal/GameLog/GameLogProjector.cs`, `BuildTokenResolvedWithNoEffectMessage` (~line 157) — add
  `TokenAction.Recycle => $"{actor} picked a Recycle token, but there was no unrolled token to swap in — nothing happened."`
- `TrashAnimal.Api/Application/GameApplicationService.cs`, `BuildTokenFizzleInfoMessage` (~line 342) — add
  `TokenAction.Recycle => "There was no unrolled token to swap in — the Recycle token resolved with no effect."`

Leave the existing `var token => …` / default arms in place as the backstop.

## B2 — StashTrash stash-mode with zero stashable cards

**Mechanism.** `TokenPhaseAllowedActionsProvider`'s `StashTrashChooseBranch` case (~line 45) offers
`TokenStashTrashDrawOne` **and** `TokenStashTrashStashMode` unconditionally.
`TokenPhaseTokenResolver.TryStashTrashEnterStashMode` (~line 136) then sets
`Step = StashTrashPickCard` with no check. If the player has no stashable card, that step's prompt list
is empty and its only possible `allowedActions` are interrupt cards — so a hand of exactly
`{Doggo, Kitteh}`, or an empty hand, yields **zero** legal actions with no way back.

**Fix approach — gate, do not fizzle.** Unlike Recycle, StashTrash always has a working alternative:
the draw branch resolves the token unconditionally (and is safe even on an empty deck, per the audit
table). So the correct fix is to stop offering an impossible choice, not to burn the token. **Firm
directive: do not add a fizzle path for StashTrash.**

### B2 changes

**B2.1 — gate the action.** `TokenPhaseAllowedActionsProvider` needs `TokenPhaseCardEligibility`, which
it does not currently take. Add it as a constructor parameter; `TokenPhaseCoordinator` already owns a
single shared `_eligibility` instance and constructs this provider, so pass that one through. Then:

```csharp
case TokenPhaseStep.StashTrashChooseBranch:
    actions.Add(GameAction.TokenStashTrashDrawOne);
    if (HasStashableHandCard())
        actions.Add(GameAction.TokenStashTrashStashMode);
    // ... existing interrupt-card additions unchanged ...
```

with a private helper:

```csharp
private bool HasStashableHandCard() =>
    _session.CurrentPlayer.Hand.Any(e => _eligibility.CanOfferCardForStashPrompt(e.Card.Name));
```

**Critical — match the prompt's filter exactly.** `TokenPhaseViewBuilder.GetStashableHandTuplesForView`
filters **only** on `CanOfferCardForStashPrompt`. It does **not** apply the `NewlyAdded` /
`TokenResolutionStartLocked` rule (that rule governs *playing cards for actions*, not stashing). Do not
add a `NewlyAdded` condition to `HasStashableHandCard` — a mismatch between this gate and the prompt
list would either re-create the deadlock (gate says yes, prompt empty) or hide a legal option.

**B2.2 — defensive guard.** In `TokenPhaseTokenResolver.TryStashTrashEnterStashMode`, after the existing
step check, reject when nothing can be stashed (the API can still submit the action even when it is not
in `allowedActions`):

```csharp
if (!_session.CurrentPlayer.Hand.Any(e => _eligibility.CanOfferCardForStashPrompt(e.Card.Name)))
{
    error = "You have no cards that can be stashed.";
    return false;
}
```

The resolver already holds `_eligibility`, so no constructor change is needed here.

**B2.3 — frontend.** `TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx`'s
`StashTrashChooseBranch` block renders **both** buttons unconditionally, so today it would still show
"Stash a card" and turn a click into a 422 error toast. Gate both buttons on `allowedActions`:

```tsx
{tokenPhase.step === 'StashTrashChooseBranch' && (
  <div className="flex gap-3">
    {allowedActions.includes('TokenStashTrashDrawOne') && (<button …>Draw a card</button>)}
    {allowedActions.includes('TokenStashTrashStashMode') && (<button …>Stash a card</button>)}
  </div>
)}
```

Keep the existing styling/labels; `allowedActions` is already a prop on this component.

## Site disposition — every affected file

| File | Disposition |
|---|---|
| `TokenPhase/Services/TokenPhaseTokenResolver.cs` | **B1.2** (Recycle fizzle), **B2.2** (stash-mode guard), **B3.1** (rename consumer), **B3.2** (add replacement to exclusion set) |
| `TokenPhase/Services/TokenPhaseTokenCompletionEngine.cs` | **B1.1** (ctor gains view builder), **B1.3** (Recycle fizzle on repeat) |
| `TokenPhase/Services/TokenPhaseAllowedActionsProvider.cs` | **B2.1** (ctor gains eligibility, gate stash-mode) |
| `TokenPhase/TokenPhaseCoordinator.cs` | pass the shared `_eligibility` into the allowed-actions provider; no other change |
| `TokenPhase/Services/TokenPhaseViewBuilder.cs` | **B3.1** (rename consumer only — `GetRecycleOptions`'s logic is unchanged, it just reads the renamed field) |
| `TokenPhase/TokenPhaseState.cs` | **B3.1** (rename `InitialTokensSnapshot` → `TokensIneligibleForRecycle`, update doc comment and ctor) |
| `TokenPhase/TokenPhaseCardEligibility.cs` | **no change** |
| `GameLog/GameLogProjector.cs` | **B1.4** (Recycle wording) |
| `TrashAnimal.Api/Application/GameApplicationService.cs` | **B1.4** (Recycle wording); no `Execute*` changes — the existing generic `resolvedWithNoEffectToken` plumbing already carries a Recycle fizzle |
| `TrashAnimal.Web/src/components/gameboard/TokenPhasePanel.tsx` | **B2.3** |
| `TrashAnimal.Web/src/api/types.ts` | **no change** — no new enum members |
| `TrashAnimal/Program.cs` (CLI) | **no change** — it drives Recycle from the options list and StashTrash from allowed actions, both of which now correctly shrink |

## Tests

Use these exact names.

**`TrashAnimal.Tests/TokenPhaseZeroOptionTokenTests.cs` (new file)**
1. `Recycle_WithAllSixTokensRolled_AutoResolvesWithNoEffect` — seed a TokenPhase whose `InitialTokensSnapshot` holds all six faces; resolve Recycle; assert it leaves `RemainingTokens`, `ActiveToken` is cleared, a `TokenResolvedWithNoEffectEvent(Recycle)` is recorded, and the step returns to `ChoosingNextToken` (or the turn ends if it was last).
2. `Recycle_WithAtLeastOneUnrolledToken_StillEntersReplacementStep` — regression: the normal path must not fizzle.
3. `MmmPieRepeatOfRecycle_WithNoOptions_AutoResolvesWithNoEffect` — the `RestartSubflow` branch (B1.3).
4. `Recycle_ZeroOptions_SurfacesFizzleTokenFromApplyAction` — assert the `out TokenAction?` signal reaches `GameSession.ApplyAction`, so the HTTP layer can produce an `InfoMessage`.
5. `StashTrash_WithOnlyDoggoAndKittehInHand_DoesNotOfferStashMode` — assert `TokenStashTrashStashMode` is absent from `GetAllowedActionsForPlayer` while `TokenStashTrashDrawOne` is present.
6. `StashTrash_WithEmptyHand_DoesNotOfferStashMode` — same assertion for the empty-hand case.
7. `StashTrash_WithAStashableCard_StillOffersStashMode` — regression against over-gating.
8. `StashTrash_EnterStashMode_RejectedWhenNothingCanBeStashed` — the B2.2 defensive guard returns `false` with an error rather than stranding.
9. `StashTrash_StashModeGateMatchesPromptList` — assert that whenever `TokenStashTrashStashMode` is offered, `TokenPhaseView.stashableHandCardsForCurrentPrompt` is non-empty after entering stash mode. This is the anti-drift test for the B2.1 "match the prompt's filter exactly" warning; include a `NewlyAdded` card in the hand so the test would fail if someone adds a `NewlyAdded` filter to only one side.

**`TrashAnimal.Api.Tests/Integration/TokenPhaseZeroOptionTokenTests.cs` (new file)**
10. `Recycle_ZeroOptions_OverHttp_PopulatesInfoMessageAndAdvances` — submit `ResolveTokenRecycle` as a `PlayActionCommand` in the all-six-rolled state; assert `succeeded == true`, `infoMessage` is non-null, and `allowedActions` no longer offers Recycle.

**Frontend**
11. `TokenPhasePanel.test.tsx` — extend: hides the "Stash a card" button when `TokenStashTrashStashMode` is absent from `allowedActions`, and shows it when present.

**Existing tests** must all still pass. `TokenPhaseTokenCompletionEngine`'s and
`TokenPhaseAllowedActionsProvider`'s constructor changes are internal; fix any construction sites the
compiler flags without weakening assertions.

## Verification

1. `dotnet build` — 0 errors.
2. `dotnet test TrashAnimal.Tests` and `dotnet test TrashAnimal.Api.Tests` — full runs. Baselines on this
   branch before this work: **111** and **75** passing.
3. `cd TrashAnimal.Web && npm run test:run && npm run build`. Baseline: **144** passing.
4. **Re-run the automated playthrough harness** at
   `C:\Users\Seth\AppData\Local\Temp\claude\C--Users-Seth-Source-Repos-TrashAnimal\c0d97294-91fd-4b72-a3be-22ab6c69bbee\scratchpad\playthrough.py`
   (needs the API running on `http://localhost:5080`). It drives 13 games across 2/3/4 players and
   reports any state with no legal move. Before this fix it reports 1 stuck event (the Recycle case);
   after, it must report **0**. Widen the seed list if you want more coverage — the harness takes seeds
   in its `__main__` block.
5. Confirm the site disposition table; report any site that differed.

## B3 — MmmPie-repeated Recycle can pick the same replacement twice (settled rule, fix required)

**The rule** (settled 2026-07-29): a Recycle pick may never select a token that has already been placed
in the player's resolution list this TokenPhase — whether it got there by the original roll *or* by an
earlier Recycle pick. Worked example: player enters TokenPhase with `{DoubleTrash, Recycle}`. MmmPie
repeats Recycle. First pick's options are every type except `Recycle`/`DoubleTrash` — i.e.
`{StashTrash, DoubleStash, Bandit, Steal}`. Player picks `StashTrash`. The **second** pick (the repeat)
must now offer only `{DoubleStash, Bandit, Steal}` — `StashTrash` is excluded too, not just `DoubleTrash`.

**Current behavior does not do this.** `TryRecycleReplacementPick` adds the chosen replacement to
`state.RemainingTokens` only. `GetRecycleOptions`/the duplicate-pick check both key off
`InitialTokensSnapshot`, which is populated once, at construction, from the original roll — a
Recycle-picked replacement never enters it. So the second pick in the worked example above would
incorrectly still offer `StashTrash`, and picking it again is a legal no-op (the `HashSet.Add` silently
does nothing, per B1's audit table entry for DoubleStash-style "empty submit is legal" — except here
the *player* loses their repeat to it, not the engine self-resolving).

**Fix — smaller than it looks.** `InitialTokensSnapshot` already has exactly two consumers, and both are
precisely the exclusion rule above (`TokenPhaseTokenResolver.cs:308`'s duplicate-pick rejection, and
`TokenPhaseViewBuilder.cs:48`'s option-list filter — see "Site disposition" for exact current line
numbers, which may have shifted after B1/B2). Making the set gain a replacement at pick time, in the one
place replacements are picked, satisfies both consumers with no other change.

**B3.1 — rename the field**, since it will no longer mean "as rolled at turn start" once B3.2 lands, and
a stale name here would misdirect the next reader (`TrashAnimal/TokenPhase/TokenPhaseState.cs`):

```csharp
/// <summary>Tokens ineligible for a Recycle pick this TokenPhase — the tokens rolled at turn start,
/// plus any token a Recycle pick has already placed into <see cref="RemainingTokens"/> since. A token
/// can never be recycled into twice, whether it arrived by roll or by an earlier Recycle pick.</summary>
public HashSet<TokenAction> TokensIneligibleForRecycle { get; } = new();
```

Rename the constructor's `InitialTokensSnapshot.Add(t)` (line 11) to match. Update both existing
consumers (`TokenPhaseTokenResolver.cs:308`, `TokenPhaseViewBuilder.cs:48`) to the new name — mechanical
rename, no behavior change from this step alone.

**B3.2 — add the replacement to the set** (`TokenPhaseTokenResolver.TryRecycleReplacementPick`, the
existing `state.RemainingTokens.Add(replacement);` line, ~line 321 pre-B1/B2 — locate by the surrounding
`if (!opts.Contains(replacement))` check, which stays unchanged):

```csharp
state.RemainingTokens.Add(replacement);
state.TokensIneligibleForRecycle.Add(replacement);
return _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
```

**Do not** add the replacement anywhere else (e.g. not in `TryFinishCurrentTokenPassOrRepeat`) — it must
happen exactly once, at the moment of picking, so a *rejected* pick (invalid replacement, wrong step,
wrong player) never pollutes the exclusion set.

**Interaction with B1 — verify, do not assume.** B1 makes a zero-option Recycle fizzle and call
`TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect` instead of entering
`RecycleChoosingReplacement`. That fizzle path never calls `TryRecycleReplacementPick`, so it cannot add
anything to `TokensIneligibleForRecycle` — correct, since no replacement was chosen. Confirm this stays
true after implementing both B1 and B3 in the same pass (it should, since B3 only touches the pick
method, not the fizzle branches) rather than assuming it from this description.

### B3 tests (add to `TokenPhaseZeroOptionTokenTests.cs`)

12. `MmmPieRepeatOfRecycle_CannotPickTheSameReplacementTwice` — reproduce the worked example exactly:
    initial tokens `{DoubleTrash, Recycle}`, MmmPie repeat, first pick `StashTrash`, assert the second
    pick's available options (`TokenPhaseView.recycleReplacementOptions`) are exactly
    `{DoubleStash, Bandit, Steal}` — `StashTrash` absent, `DoubleTrash`/`Recycle` absent. Then assert
    submitting `StashTrash` again is rejected with an error (not silently accepted).
13. `Recycle_PickedReplacement_ExcludedFromASecondRecycleWithinTheSameChain` — same shape as #12 but via
    two chained Recycle picks under a single MmmPie repeat (if the domain allows re-triggering Recycle
    within one repeat; if `RestartSubflow`'s Recycle case cannot itself loop, merge this into #12 rather
    than writing a case that cannot occur — verify before writing).
14. `Recycle_RejectedPick_DoesNotPolluteExclusionSet` — attempt an invalid replacement (already in
    `InitialTokensSnapshot`/rolled), confirm it's rejected, then confirm a *valid* subsequent pick still
    lists every option it should (i.e. the rejected attempt left no trace in the exclusion set).

Add file-line reference updates to the plan's "Site disposition" table below once B1/B2 land, since B3
edits two of the same files (`TokenPhaseTokenResolver.cs`, `TokenPhaseViewBuilder.cs`) plus one more
(`TokenPhaseState.cs`) not previously listed.

## Out of scope — related observations, deliberately not fixed

- Unifying the first-pick vs MmmPie-repeat Steal entry points (deferred decision 3 from
  `steal-token-mmmpie-repeat-fix.md`).
