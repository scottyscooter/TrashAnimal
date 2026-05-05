---
name: fix_subplan_links
overview: Move the 8 subplan files from the user-level plans directory into the workspace .cursor/plans directory and fix all broken links so subplans are openable from the main plan.
todos:
  - id: move-files
    content: Copy 8 subplan files from user-level to workspace .cursor/plans and delete originals
    status: completed
  - id: fix-main-plan-link
    content: Fix the Implementation Phases link in cli_to_rest_web_migration_71859257.plan.md
    status: completed
  - id: fix-index-links
    content: Fix all 7 phase links in trashanimal_api_implementation_phases.plan.md
    status: completed
isProject: false
---

# Fix Subplan Links

## Problem

The 8 subplan files were written to `C:\Users\Seth\.cursor\plans\` (user-level) instead of `C:\Users\Seth\Source\Repos\TrashAnimal\.cursor\plans\` (workspace-level). Plan links only resolve within the same directory.

## Changes

### 1. Move subplan files into workspace plans directory

Copy each of the following from `C:\Users\Seth\.cursor\plans\` into `C:\Users\Seth\Source\Repos\TrashAnimal\.cursor\plans\` and delete the originals:

- `trashanimal_api_implementation_phases.plan.md`
- `trashanimal_api_phase1_host_and_app_layer.plan.md`
- `trashanimal_api_phase2_command_contracts.plan.md`
- `trashanimal_api_phase3_session_lifecycle.plan.md`
- `trashanimal_api_phase4_cli_decouple.plan.md`
- `trashanimal_api_phase5_realtime.plan.md`
- `trashanimal_api_phase6_frontend_contract.plan.md`
- `trashanimal_api_phase7_tests.plan.md`

### 2. Fix link in main plan

In [`cli_to_rest_web_migration_71859257.plan.md`](.cursor/plans/cli_to_rest_web_migration_71859257.plan.md), update the Implementation Phases link from:

```
[TrashAnimal.Api Implementation Phases](trashanimal_api_implementation_phases.plan.md)
```

to:

```
[TrashAnimal.Api Implementation Phases](.cursor/plans/trashanimal_api_implementation_phases.plan.md)
```

### 3. Fix links in the index subplan

In `trashanimal_api_implementation_phases.plan.md`, update all 7 phase links from bare filenames to workspace-relative paths (e.g. `trashanimal_api_phase1_host_and_app_layer.plan.md` → `.cursor/plans/trashanimal_api_phase1_host_and_app_layer.plan.md`).
