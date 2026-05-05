---
name: trashanimal_api_implementation_phases
overview: Index of per-phase subplans for adding TrashAnimal.Api on top of the TrashAnimal engine project.
isProject: false
---

# TrashAnimal.Api Implementation Phases

## Project Structure

- `TrashAnimal` — engine/business layer (no changes to rules or domain logic)
- `TrashAnimal.Api` — new ASP.NET Core project; references `TrashAnimal` as a project dependency
- `TrashAnimal.Api.Tests` — new project for API integration and contract tests
- `TrashAnimal.Tests` — existing engine tests; do not modify

## Phases

- [Phase 1: Introduce TrashAnimal.Api and Application Layer](.cursor/plans/trashanimal_api_phase1_host_and_app_layer.plan.md)
- [Phase 2: Standardize Command Contracts](.cursor/plans/trashanimal_api_phase2_command_contracts.plan.md)
- [Phase 3: Session Lifecycle and Concurrency Guardrails](.cursor/plans/trashanimal_api_phase3_session_lifecycle.plan.md)
- [Phase 4: Replace CLI Interaction Paths](.cursor/plans/trashanimal_api_phase4_cli_decouple.plan.md)
- [Phase 5: Add Realtime Updates](.cursor/plans/trashanimal_api_phase5_realtime.plan.md)
- [Phase 6: Frontend Integration Contract](.cursor/plans/trashanimal_api_phase6_frontend_contract.plan.md)
- [Phase 7: Test Strategy](.cursor/plans/trashanimal_api_phase7_tests.plan.md)
- [Phase 8: Local Development Secrets](.cursor/plans/trashanimal_api_phase8_local_secrets.plan.md)
- [Phase 9: Authentication](.cursor/plans/trashanimal_api_phase9_auth.plan.md)
