---
name: trashanimal_api_phase6_frontend_contract
overview: Define the minimal API contract the web frontend consumes — endpoints, SignalR events, hidden-information boundaries, and OpenAPI documentation.
todos:
  - id: endpoint-spec
    content: Document all HTTP endpoints with request/response shapes
    status: pending
  - id: signalr-events
    content: Document SignalR hub events and expected client behavior
    status: pending
  - id: hidden-info-boundary
    content: Confirm per-player GameView projections enforce hidden-information rules
    status: pending
  - id: openapi-setup
    content: Add Microsoft.AspNetCore.OpenApi package, wire AddOpenApi/MapOpenApi in Program.cs, and add Scalar UI for local browsing
    status: pending
isProject: false
---

# Phase 6: Frontend Integration Contract

## Goal

Define the complete API surface the frontend depends on so it can be built or integrated against a stable, documented contract.

## HTTP Endpoints

- `POST /games` — create a new game; returns `gameId` and initial per-player view.
- `GET /games/{gameId}/view?playerSeat={n}` — returns the calling player's `GameView`, current `GameState`, allowed `GameAction` list, and current `revision`.
- `POST /games/{gameId}/commands` — submit an action; body contains `playerSeat`, `action` (string enum), and any optional payload (card ids, victim seat, token choice). Returns updated view or error with reason.
- `GET /games/{gameId}/result` — available when `GameState == GameEnded`; returns `GameEndResult` with score summary.

## SignalR Events

- Client connects to `GameHub` on game load and joins the game group for `gameId`.
- Server pushes `GameUpdated` event to the group after each successful command:

```json
{
  "gameId": "...",
  "revision": 12,
  "actingPlayerSeat": 1,
  "currentGameState": "TokenPhase"
}
```

- Client receives `GameUpdated` and calls `GET /games/{gameId}/view` to refresh its local state.
- On SignalR reconnect, client compares its cached `revision` to the latest view and refreshes if behind.

## Hidden-Information Boundary

- `GET /games/{gameId}/view` always returns the per-player projection from `GameSession.GetViewForPlayer(playerSeat)`.
- Opponent hand contents (card identities) must never appear in the response — only hand count or equivalent public info exposed by `GameView`.
- `TrashAnimal.Api` must not expose `GameSession`, `Player`, `Hand`, `Deck`, or `StashPile` directly; all reads go through the engine's view types.

## OpenAPI

ASP.NET Core 10 ships first-party OpenAPI support via `Microsoft.AspNetCore.OpenApi`. No third-party Swashbuckle package is required.

### 6.4 Add OpenAPI Document Generation

- Add `Microsoft.AspNetCore.OpenApi` as a `PackageReference` in `TrashAnimal.Api.csproj`.
- Call `builder.Services.AddOpenApi()` in `Program.cs` (or the appropriate `ServiceCollectionExtensions` method).
- Call `app.MapOpenApi()` inside an `if (app.Environment.IsDevelopment())` guard — this exposes the live document at `/openapi/v1.json`. The endpoint must never be reachable in production.

### 6.5 Add Scalar UI for Local Browsing

- Add the `Scalar.AspNetCore` NuGet package (the idiomatic .NET 10 interactive API UI).
- Map Scalar inside the same Development guard: `app.MapScalarApiReference()` — accessible at `/scalar/v1` by default.
- Scalar reads from `/openapi/v1.json` automatically; no additional configuration is needed for the baseline setup.

### 6.6 Build-Time Document Generation (Optional)

- Add `Microsoft.Extensions.ApiDescription.Server` as a build-only `PackageReference`.
- Set `<OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>` in a `PropertyGroup` so the document is serialized to `obj/` on every build.
- This enables diffing the spec in CI to catch unintended contract changes.

## Constraints

- Enum values in all requests and responses are strings (enforced from Phase 1).
- `gameId` and `playerSeat` are required on every stateful request.
- The frontend must treat `GET /games/{gameId}/view` as the single source of truth; SignalR notifications are triggers, not data payloads.
- `app.MapOpenApi()` and `app.MapScalarApiReference()` must be restricted to the Development environment; exposing the spec in production is a security risk.
