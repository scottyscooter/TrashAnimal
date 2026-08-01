# Notes for a future planning session: two more MmmPie-adjacent bugs

> **Status: Note 1 resolved 2026-07-30 on `fix/mmmpie-yumyum-and-steal-log-messages`.** Root cause
> confirmed exactly as hypothesized below: `TokenPhaseTokenCompletionEngine.RestartSubflow`'s
> `DoubleStash` case just re-sets `state.Step = DoubleStashChoosingCards` (domain-side is correct,
> no bug there) — it's purely `TokenPhasePanel`'s local `doubleStashSelection` state surviving the
> re-entry. Fix: `TokenPhasePanel.tsx`'s submit `onClick` now clears `doubleStashSelection`
> immediately alongside dispatching `onDoubleStashSubmit`, so a repeat prompt (same component
> instance, `step` never leaves `DoubleStashChoosingCards` across the repeat) always starts empty.
> Covered by a new `TokenPhasePanel.test.tsx` case. Note 2 turned out not to be a separate bug:
> `TokenPhaseAllowedActionsProvider` already correctly gates `PlayMmmPieTokenPhase` per-step
> (verified — it's offered during `StashTrashPickCard`/`DoubleStashChoosingCards` by design, so a
> player can queue a repeat before finishing the current token's own prompt; see
> `steal-token-mmmpie-repeat-fix.md` decision 2), and the "separate bug" the note anticipated was
> this same stale-selection issue manifesting once the repeat's `DoubleStashChoosingCards` prompt
> reopened. No further Note 2 fix needed.
>
> Original notes below, for historical context.

> **Status: raw notes, not a plan.** Found 2026-07-29 while manually playing after the
> `token-zero-option-deadlocks-fix.md` fix landed. Not yet diagnosed to the rigor of that plan (no
> full site enumeration, no confirmed root cause, no settled decisions) — do that work before
> implementing. Filed here per this repo's "new plan documents go in `.claude/docs/plans/`" convention
> so it isn't lost, and because it's the fourth bug in the same MmmPie-repeat family found this session
> (after Steal-never-exhausts, the general InfoMessage gap, and the Recycle/StashTrash zero-option
> deadlocks) — worth treating as one audit rather than another one-off patch.

## Note 1: MmmPie + DoubleStash — second card-selection window is unusable

**Reported symptom:** playing MmmPie then resolving DoubleStash works the first time. On the repeat
(second resolution), the "pick 0–2 cards to stash" prompt reappears, but the player cannot select any
card — clicking does nothing, and the game deadlocks (no `EndTurn`/progress possible).

**Working hypothesis, not yet confirmed against a live repro:** `TokenPhasePanel.tsx`'s
`doubleStashSelection` (`useState<string[]>`, line ~40) is local component state, reset only by
`toggleDoubleStashCard`'s own logic — never explicitly cleared when `tokenPhase.step` re-enters
`DoubleStashChoosingCards` for a second time. `toggleDoubleStashCard` caps selection at 2
(`if (current.length >= 2) return current;`). If the first resolution left 0–2 card IDs selected (e.g.
the player picked 2 cards, or clicked toward 2 but the submit button auto-cleared some but not all
paths), those stale IDs — now for cards no longer in the current prompt — could still count toward the
cap, silently blocking every click on the second prompt with no visible feedback.

**What the next session needs to check before proposing a fix, not assumed here:**
- Does the *domain* correctly reset/track anything DoubleStash-repeat-related, or is this purely a
  frontend state bug? (Compare against how `TokenPhaseTokenCompletionEngine.RestartSubflow`'s
  `DoubleStash` case behaves — it just sets `Step = DoubleStashChoosingCards` again, same pattern as
  StashTrash/Recycle, so likely no domain-side issue — but verify, don't assume from this note.)
- Does `TokenPhasePanel` actually persist across the repeat (same mounted instance), or does something
  higher up remount it between resolutions? The bug only exists if it persists.
- Reproduce it directly (unit-level is hard since this is React local state — likely needs an RTL test
  simulating two sequential `DoubleStashChoosingCards` renders) before writing a fix plan.
- Is the actual bug "stale IDs count toward the cap" or something else — e.g. `onDoubleStashSubmit`
  itself failing on the second submission for a domain-side reason? Check both.

**Likely fix shape** (unconfirmed): clear `doubleStashSelection` when `tokenPhase.step` transitions into
`DoubleStashChoosingCards` (e.g. a `useEffect` keyed on entering the step, or reset it right after a
successful `onDoubleStashSubmit` call rather than relying on unmount). Needs the next session's own
diagnosis pass, not this guess, before implementing.

## Note 2: "Play MmmPie" button remains clickable while mid-resolving a token's own prompt

**Reported symptom:** the "Play MmmPie (repeat this token)" button stays clickable while the player is
in the middle of a token's own interactive sub-prompt (e.g. mid-selecting DoubleStash cards, or on
`StashTrashPickCard`) — not just at `ChoosingNextToken`. Clicking it there leads to a separate bug
(unspecified symptom — not detailed in the report).

**User's own framing, worth preserving verbatim:** this may be partly superseded by planned UI work
where playable cards are invoked by **clicking the card itself in hand** (the pattern `PlayerHand.tsx`
already uses for Feesh) rather than a separate "Play X" button. If MmmPie moves to that pattern, this
specific bug's surface may change or disappear on its own. **Action for the next session: don't treat
this as moot — re-check it specifically once the click-to-play-from-hand work lands, and only close it
out if it's confirmed fixed by that change, not by assumption.**

**What's already known from this session's domain work, for context:** `TokenPhaseInterruptCardPlay`
already blocks *stacking* a second MmmPie while `state.ResolveTokenTwice` is true (`CanPlayMmmPie`
checks `!state.ResolveTokenTwice`, added in `steal-token-mmmpie-repeat-fix.md`'s decision 2). That
guard is about the *domain's* eligibility check (`GetAllowedActionsForPlayer`), which should already
make `PlayMmmPieTokenPhase` absent from `allowedActions` during a pending repeat. If the frontend button
is still clickable when it shouldn't be, first check whether: (a) the button isn't actually gated on
`allowedActions.includes('PlayMmmPieTokenPhase')` in every step's render branch (it's added
independently in each of `TokenPhasePanel`'s per-step blocks — `StashTrashChooseBranch`,
`StashTrashPickCard`, `DoubleStashChoosingCards` each repeat the same `if
(allowedActions.includes('PlayMmmPieTokenPhase')) …` block near the top of the file, so a copy-paste
drift between them is plausible and should be checked file-by-file), or (b) the domain is correctly
excluding it from `allowedActions` but the *symptom* is about something else entirely (e.g. MmmPie being
legitimately playable — different token, different repeat not yet pending — but doing something wrong
when played mid-prompt). Don't assume which without checking `allowedActions` on a live repro.

## Suggested next-session approach

Given this is the fourth MmmPie-repeat-family bug this session, consider doing one more completeness
pass — audit every `TokenPhaseStep`'s interaction with `ResolveTokenTwice`/MmmPie eligibility and
every piece of *frontend* local state that survives a step re-entry (not just domain state, which
got the thorough audit in the last two plans) — rather than fixing Note 1 and Note 2 as two more
one-off patches. `doubleStashSelection` is very likely not the only piece of `TokenPhasePanel`
local state that assumes "this step is only ever entered once per turn."
