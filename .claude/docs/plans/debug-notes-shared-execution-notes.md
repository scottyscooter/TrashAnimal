# Debug-Notes Plans — Shared Execution Notes

Shared coordination for [Plan A](debug-notes-a-correctness-and-blocked-states.md), [Plan B](debug-notes-b-card-selection-interaction-model.md), and [Plan C](debug-notes-c-steal-information-leak.md). Read this before dispatching any agent.

---

## Lock order (do these in this sequence)

1. **Land `fix/token-phase-steal-exhaustion` on `main` first — hard blocker for all three plans, confirmed by direct inspection, not just an assumption.**

   Verified against the working tree on 2026-07-29: every file all three plans touch is currently dirty and uncommitted on this branch, and several are files that **don't exist on `main` at all**:
   - `TrashAnimal.Web/src/components/InfoBadge.tsx` — the component Plan A's A4 fixes — is untracked; `git show main:...InfoBadge.tsx` fails. It was introduced on this branch.
   - `TrashAnimal/GameSession.Views.cs`, `GameSession.TokenPhaseDelegation.cs`, `TokenPhase/Services/TokenPhaseStealHandler.cs` — new, untracked; `GameSession`'s internals were restructured, not just patched.
   - `TrashAnimal.Api/Application/GameCommandResult.cs` on `main` has **no `InfoMessage` field** (confirmed via `git show main:...`) — Plan A's A1 and Plan B's B1 both assume it exists.
   - `PlayerHand.tsx`, `TokenPhasePanel.tsx`, `GameBoardPage.tsx`, `GameSession.cs`, `GameSession.ApiSupport.cs`, every file under `TokenPhase/**`, `GameLogEvent.cs`, `GameLogProjector.cs` — all modified, uncommitted, on this branch.

   Every plan section's "Verified current state" was written by reading the working tree *as it stands on this branch*, so the plans are only accurate once this branch's content is what `main` actually contains. Starting a plan section off `main` today means working against code about to be restructured out from under it; starting a plan section stacked on this branch means building on top of 55 files of unreviewed, uncommitted work. **Land this branch first.**
2. **Plan A section A0** (discovery, read-only) — produces the checklist A1 needs.
3. **Plan A section A4** (InfoBadge fix) — self-contained, unblocks A2's badge rollout.
4. **Plan A section A2** (playability contract) — the critical-path contract change. Plan B is blocked until this is on `main`.
5. **Plan A section A1** (announcements) — needs A0's checklist; independent of A2/A4/B/C after that.
6. **All of Plan B** — starts after A2 lands. B0/B3, B1, B2, B4 can then run per Plan B's internal order.
7. **Plan C** — independent of A and B; can start any time after step 1 and run entirely in parallel with A and B.

**Step 1 is the hard blocker for everything below — including Plan C**, despite Plan C's own doc saying it's independent of A/B. That independence is only true *relative to A and B*; it does not exempt Plan C from waiting on step 1, since Plan C's target files (`StealAttempt.cs`, `StealPrompt.tsx`) are also dirty on the branch. Steps 2–7 have internal ordering among themselves; C can run alongside A and B, but only after step 1.

---

## Parallelism matrix

| Phase | Concurrent lanes |
|---|---|
| After step 1 | A0 (read-only sweep) ∥ A4 (frontend, self-contained) ∥ Plan C start |
| After A4 lands | A2 (needs `PlayerHand.tsx` free) ∥ A1 discovery-done ∥ Plan C continues |
| After A2 lands | B0/B3 ∥ B2 backend ∥ A1 impl ∥ Plan C continues |
| After B0 lands | B1 ∥ B2 frontend wire ∥ B4 |

**Do not exceed 3 concurrent worktrees on the frontend.** `GameBoardPage.tsx` is a coordination bottleneck across A1, A2, B0, B1, B2 — a fourth parallel lane just guarantees rebases.

---

## Contract-change coordination

Three plans additively extend `TrashAnimal.Web/src/api/types.ts` and the corresponding C# records. All are additive (new fields on existing records), so `src/api/contracts.test.ts` catches drift immediately. Order the merges so contract tests fail loudly rather than silently drift:

| Contract change | Plan | C# file | Frontend mirror |
|---|---|---|---|
| `HandCardView` — add `PlayableAs`, `UnplayableReason` | A2 | `TrashAnimal/GameView.cs` | `src/api/types.ts` `HandCardView` |
| `GameLogEntryView` — add `AffectedPlayerSeats?` | A1 (option ii) | `TrashAnimal/GameLog/GameLogProjector.cs` | `src/api/types.ts` `GameLogEntryView` |
| `OwnStashView` — replace `FaceDownCount` with `FaceDownCards` | B2 | `TrashAnimal/GameView.cs` | `src/api/types.ts` `OwnStashView` |
| No wire-shape change (order semantics only) | C1, C3 | `TrashAnimal/StealPickSlot*`, `TrashAnimal/StealAttempt.cs` | — |

Every contract change PR must update `contracts.test.ts` in the same commit. A contract change merged without the frontend mirror will red-line every frontend PR until fixed.

---

## Worktree hygiene

- One worktree per branch. Never share a worktree between two active agents.
- Before dispatching an agent, `git worktree list` to confirm the worktree exists and is on the expected branch.
- Agents receive their branch/worktree in the prompt — do **not** let them `git checkout` in the primary worktree.
- Cleanup: when an agent finishes and the PR merges, remove the worktree explicitly (`git worktree remove <path>`). Stale worktrees are the #1 cause of "why is this branch behaving weirdly."

---

## Agent-prompt template (per section)

When actually dispatching, brief each agent with:

1. **Which plan doc and which section** (`.claude/docs/plans/debug-notes-<x>-<slug>.md#<section-id>`)
2. **Which branch to work on** (from the plan's Branch topology)
3. **Which worktree** (absolute path)
4. **Files-in-scope list** (from the plan's conflict-zone map — makes the agent's blast radius explicit)
5. **Which sibling section it depends on being merged**, and how to verify (`git log --oneline origin/main` for the dependency's expected commit subject)
6. **Live-verification requirement** — every plan section that touches game rules needs a browser playthrough per the notes' meta section, not just tests

Skip the template only for A0 / C2 (read-only research tasks) — they need a different brief.

---

## What is deliberately *not* here

- **Issue #4 (response timers on Bandit/YumYum modals)** — excluded during scoping; needs product decisions before it becomes a plan.
- **Issue #7 (steal token exhaustion)** — resolved by `fix/token-phase-steal-exhaustion`, marked Resolved in `DEBUG_NOTES.md`.
- **Issue #8 (steal token with no targets)** — resolved by `fix/token-phase-steal-exhaustion`. Verify during the step-1 merge, not as a separate plan section.
- **Polish/nits (#10, #11, #13, #15, #17)** — deferred by the user; not covered by these plans.
