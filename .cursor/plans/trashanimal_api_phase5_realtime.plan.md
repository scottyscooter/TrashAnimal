---
name: trashanimal_api_phase5_realtime
overview: Add a SignalR hub to TrashAnimal.Api so connected clients receive push notifications after each successful game command and can refresh their view without polling.
todos:
  - id: signalr-hub
    content: Add SignalR hub with per-game groups in TrashAnimal.Api
    status: done
  - id: update-publisher
    content: Implement GameUpdatePublisher that notifies the game group after each successful mutation
    status: done
  - id: client-reconnect
    content: Define reconnect/missed-update strategy using session revision
    status: done
isProject: false
---

# Phase 5: Add Realtime Updates

## Goal

Push lightweight notifications to all connected players after each successful command so the frontend stays in sync without polling, while keeping REST authoritative for command acceptance.

## Tasks

### 5.1 Add SignalR Hub

- Add the `Microsoft.AspNetCore.SignalR` package to `TrashAnimal.Api`.
- Create a `GameHub` that clients connect to on game load.
- On connect, add the client to a SignalR group keyed by `gameId` so notifications are scoped per game.

### 5.2 Implement GameUpdatePublisher

- Create `GameUpdatePublisher` in `TrashAnimal.Api`, injected into `GameApplicationService`.
- After every successful engine mutation (command accepted by `ApplyAction`/`Try*`), call `GameUpdatePublisher.NotifyAsync(gameId, updateEnvelope)`.
- `updateEnvelope` is a lightweight DTO containing:
  - `gameId`
  - `revision` (monotonic, incremented per successful command)
  - `actingPlayerSeat`
  - `currentGameState` (string enum)
- Clients receive the envelope and trigger a `GET /games/{gameId}/view` refresh — the hub does not push full game state directly.

### 5.3 Reconnect and Missed-Update Strategy

- Include `revision` in every `GET /games/{gameId}/view` response.
- On SignalR reconnect, the client compares its last-known revision to the latest view revision and refreshes if behind.
- No event replay is required for milestone 1; a full view re-fetch on reconnect is sufficient.

## Constraints

- REST endpoints remain the authoritative path for accepting commands; SignalR is push-only and read-only from the client's perspective.
- `GameHub` must not accept game commands or mutate state directly.
