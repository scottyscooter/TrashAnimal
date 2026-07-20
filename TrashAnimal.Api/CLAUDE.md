# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in `TrashAnimal.Api`. See the [repo-root CLAUDE.md](../CLAUDE.md) for shared commands, standards, and cross-project context, and [TrashAnimal/CLAUDE.md](../TrashAnimal/CLAUDE.md) for the domain engine this project wraps.

## Project Overview

**TrashAnimal.Api** is the ASP.NET Core 10 REST API + SignalR hub that exposes the `TrashAnimal` domain engine over HTTP for multiplayer sessions. It holds no game rules itself — it validates HTTP-shaped input, drives `GameSession` (via `GameSession.ApiSupport.cs`'s explicit-choice methods), and projects results back into DTOs. Game state is **in-memory only**; there is no persistence, so all games are lost on restart.

## Folder Structure

- **`Application/`** — `GameApplicationService` (the bridge from HTTP/SignalR to the domain engine — see below), plus result records `GameCommandResult` and `GameCreationResult`.
- **`Contracts/Requests/`** — `CreateGameRequest(PlayerNames, DieSeed?)`, `SubmitCommandRequest(PlayerSeat, Action, CardId?, CardIds?, RecycleReplacement?, VictimSeat?)`.
- **`Contracts/Responses/`** — `GameCreationResponse`, `PlayerViewResponse`, `GameCommandResponse` (with `FromSuccess`/`FromFailure` factories), `GameResultResponse`. These reuse domain types (`GameView`, `GameAction`, `GameEndScoreLine`, etc.) directly as DTO members — there is no separate API-side mapping layer.
- **`Controllers/`** — `GamesController` (see Endpoints below).
- **`Hubs/`** — `GameHub`, the push-only SignalR hub (see SignalR below).
- **`Sessions/`** — server-side in-memory game storage: `IGameSessionRepository` / `InMemoryGameSessionRepository` (`ConcurrentDictionary<Guid, GameSessionEntry>`, singleton), `GameSessionEntry` (wraps `GameSession` + `Die` + a `Revision` counter + a per-session `SemaphoreSlim` lock).
- **`Startup/Options/`** — `GameApplicationServiceOptions` (`StartingHandCounts`, default `[3,4,5,6]`, bound from config).
- **`Startup/Validation/`** — `GameApplicationServiceOptionsValidator` (`IValidateOptions<T>`; fails if hand-count list isn't length 2–4).
- **`Startup/`** — `ServiceCollectionExtensions` (`RegisterOptions()`, `RegisterValidators()`).
- **`Updates/`** — the push-notification abstraction: `IGameUpdatePublisher`, `GameUpdateEnvelope(GameId, Revision, ActingPlayerSeat, CurrentGameState)`, `SignalRGameUpdatePublisher` (prod, pushes to the SignalR group), `StubGameUpdatePublisher` (no-op, for tests).

## REST Endpoints (`GamesController`, route base `games`)

| Method & Route | Purpose | Response |
|---|---|---|
| `POST /games` | Create a game (`CreateGameRequest`; `PlayerNames.Count` must be 2–4) | 201 `GameCreationResponse`, `Location` → `GET /games/{id}/view?playerSeat=0` |
| `GET /games/{gameId}/view?playerSeat={int}` | Get one player's view | 200 `PlayerViewResponse` or 404 |
| `POST /games/{gameId}/commands` | Submit a game command (`SubmitCommandRequest`) | 200 `GameCommandResponse` on success, 422 on rule rejection, 404 if game not found |
| `GET /games/{gameId}/result` | Final scoreboard (only valid once `GameState.GameEnded`) | 200 `GameResultResponse` or 404 |

`SubmitCommandRequest.Action` is the primary discriminator; which optional field is populated depends on the action (`CardId` for Feesh play / card picks, `VictimSeat` for Shiny / token-steal resolution, `CardIds` for double-stash, `RecycleReplacement` for recycle picks) — see the XML doc on `GamesController.SubmitCommand` for the full table.

## GameApplicationService (`Application/`)

The single chokepoint between transport and domain:
- `CreateGameAsync`, `GetViewAsync`, `DispatchCommandAsync` (the one the controller calls — internally routes based on request shape/`GameState`/`TokenPhaseStep`), plus narrower methods (`SubmitActionAsync`, `CompleteStealWithCardAsync`, `SubmitBanditPassAsync`, `SubmitBanditStashAsync`, `SubmitDoubleStashAsync`, `SubmitStashTrashPickAsync`, `SubmitRecyclePickAsync`, `GetRecycleOptionsAsync`, `GetGameEndResultAsync`).
- Every mutation runs inside `WithSessionLockAsync`, which acquires `GameSessionEntry.Lock` (a per-session `SemaphoreSlim`) for the full read-mutate-respond cycle, preventing concurrent corruption of a single game's state.
- On success: increments `entry.Revision`, publishes a `GameUpdateEnvelope` via `IGameUpdatePublisher`, then projects a fresh view/allowed-actions for the acting player.
- Uses `GameApplicationServiceOptions.StartingHandCounts` to size hands at creation based on player count.

## SignalR (`GameHub`, mounted at `/hubs/game`)

**Push-only** — the hub's own doc comments state game commands must never be submitted through it, only through the REST endpoints above.

- Client-invokable: `JoinGameAsync(Guid gameId)`, `LeaveGameAsync(Guid gameId)` — add/remove the connection to/from group `"game:{gameId}"`.
- Server→client event: `"GameUpdated"` (`GameHub.GameUpdatedEvent`), payload `GameUpdateEnvelope { GameId, Revision, ActingPlayerSeat, CurrentGameState }`. It is a **trigger only** — on receiving it, re-fetch full state via `GET /games/{gameId}/view`.
- Recommended client flow: connect → register `GameUpdated` handler → `JoinGameAsync(gameId)` → on `GameUpdated`, re-fetch the view → compare cached `Revision` against the fresh view's revision on reconnect (to detect missed updates) → `LeaveGameAsync` on navigating away / game end.
- Enums are serialized as strings over the hub too (`AddJsonProtocol` + `JsonStringEnumConverter`), consistent with REST.

## CORS

`Program.cs` registers a named `"Frontend"` CORS policy (`AddCors`/`UseCors`) allowing the origins listed in `CorsOptions:AllowedOrigins` (config-bound, validated on start via `CorsOptionsValidator` — requires at least one origin), with `AllowAnyHeader`/`AllowAnyMethod`/`AllowCredentials` (credentials are required for SignalR's negotiate/websocket handshake). `appsettings.json` and `appsettings.Development.json` both default this to `http://localhost:5173` (the Vite dev server for `TrashAnimal.Web`); update the setting per-environment if the frontend origin changes.

## Program.cs / Configuration

- Pipeline: console logging → `RegisterOptions()`/`RegisterValidators()` (bind + `ValidateOnStart()` for `GameApplicationServiceOptions`) → DI registrations (`IGameSessionRepository` singleton, `IGameUpdatePublisher` scoped, `GameApplicationService` scoped) → `AddControllers().AddJsonOptions(...)` + `ConfigureHttpJsonOptions(...)` both wired with `JsonStringEnumConverter()` (**all enums serialize as strings**, MVC and minimal APIs alike) → `AddSignalR().AddJsonProtocol(...)` (same enum converter) → `MapControllers()` → `MapHub<GameHub>("/hubs/game")`.
- OpenAPI: `AddOpenApi()` always registered; `MapOpenApi()` (`/openapi/v1.json`) and `MapScalarApiReference()` (`/scalar/v1`) only mapped when `Environment.IsDevelopment()`.
- `public partial class Program { }` is exposed for `WebApplicationFactory<Program>` in `TrashAnimal.Api.Tests`.
- `appsettings.json`: `GameApplicationServiceOptions.StartingHandCounts = [3,4,5,6]`; `CorsOptions.AllowedOrigins = ["http://localhost:5173"]`; logging `Information` default, `Warning` for `Microsoft.AspNetCore`, `Information` for `TrashAnimal.Api`.
- `appsettings.Development.json`: overrides `CorsOptions.AllowedOrigins` (same default) and logging — `Debug` default and for `TrashAnimal.Api`, `Information` for `Microsoft.AspNetCore`.
- `TrashAnimal.Api.csproj`: `Microsoft.NET.Sdk.Web`, `net10.0`, `Nullable`/`ImplicitUsings` enabled, `UserSecretsId = 41448837-7d38-4d93-ad0c-2f5aa1557cab`, references `TrashAnimal.csproj`, packages `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore`.

## Running

```bash
dotnet run --project TrashAnimal.Api
```

Starts with `ASPNETCORE_ENVIRONMENT=Development` by default (enables OpenAPI/Scalar and user secrets). `Properties/launchSettings.json` pins the dev URL to `http://localhost:5080` (the `http` profile's `applicationUrl`), so `TrashAnimal.Web`'s `VITE_API_BASE_URL` fallback has a stable, predictable target — update both if this port ever changes. See the [repo-root CLAUDE.md](../CLAUDE.md) for the local secrets workflow (`dotnet user-secrets ...`).
