---
name: trashanimal_api_phase9_auth
overview: Replace the placeholder playerSeat query parameter with verified caller identity so game commands are authorized against a real authenticated user, resolving the deferred item from Phase 3.
todos:
  - id: jwt-bearer-middleware
    content: Add JwtBearer middleware configured for the chosen auth provider (Firebase or equivalent)
    status: pending
  - id: player-identity-resolver
    content: Define IPlayerIdentityResolver abstraction and a concrete implementation that maps authenticated UID to playerSeat
    status: pending
  - id: session-entry-uid-map
    content: Update GameSessionEntry to store a UID-to-seat mapping populated at game creation time
    status: pending
  - id: remove-seat-from-requests
    content: Remove playerSeat from request bodies and query strings; derive it from identity in GameApplicationService
    status: pending
  - id: authorize-endpoints
    content: Apply [Authorize] to all game controllers so unauthenticated requests return 401
    status: pending
  - id: update-phase8-keys
    content: Add auth provider config keys to the Expected Secret Keys table in Phase 8
    status: pending
isProject: false
---

# Phase 9: Authentication

## Goal

Replace the placeholder `playerSeat` parameter (introduced in Phase 3) with verified caller identity. Every game command must be tied to an authenticated user; the engine seat index is resolved internally from the caller's identity, not supplied by the client.

## Auth Provider Decision

Firebase Authentication is the current candidate. Firebase issues standard JWTs that ASP.NET Core validates as Bearer tokens using `Microsoft.AspNetCore.Authentication.JwtBearer` — no Firebase-specific SDK is required on the server. The tasks below call out which steps are Firebase-specific so the plan can be adapted if a different provider (e.g., Azure AD B2C, Auth0) is chosen later.

## Tasks

### 9.1 Add JWT Bearer Middleware

*(Firebase-specific — see note below)*

- Add `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package to `TrashAnimal.Api`.
- Register authentication and authorization services in `ServiceCollectionExtensions`:

```csharp
services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{projectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{projectId}",
            ValidateAudience = true,
            ValidAudience = projectId,
        };
    });
services.AddAuthorization();
```

- Read `projectId` from `IConfiguration` using the key `Authentication:Firebase:ProjectId` (stored as a user secret from Phase 8).
- Firebase's public signing keys are fetched automatically from the JWKS URI; no manual key management is required.
- Add `app.UseAuthentication()` and `app.UseAuthorization()` to the middleware pipeline in `Program.cs`, before `app.MapControllers()`.

**If switching providers:** Replace the `Authority`/`ValidIssuer`/`ValidAudience` values with those of the new provider. The rest of the middleware setup is provider-agnostic.

### 9.2 Define IPlayerIdentityResolver

Create an `IPlayerIdentityResolver` abstraction in `TrashAnimal.Api` so `GameApplicationService` does not depend on any auth-provider type:

```csharp
public interface IPlayerIdentityResolver
{
    string ResolveUserId(ClaimsPrincipal principal);
}
```

Provide a `ClaimsPrincipalPlayerIdentityResolver` that reads the `sub` claim (or Firebase's `user_id` claim) from the `ClaimsPrincipal`. Inject `IPlayerIdentityResolver` into `GameApplicationService` via the constructor.

### 9.3 Update GameSessionEntry

Add a `IReadOnlyDictionary<string, int> UidToSeatMap` property to `GameSessionEntry`. This mapping is populated at game creation time — the `POST /games` request body must include the Firebase UIDs of each player, and they are assigned to seat indices 0..N-1 in order.

`GameApplicationService.CreateGameAsync` sets the map and stores it with the session. Subsequent commands look up the calling user's UID in the map to determine their `playerSeat`.

### 9.4 Remove playerSeat from Request Contracts

- Remove `playerSeat` from command request bodies and the `GET /games/{gameId}/view` query string.
- In `GameApplicationService`, call `IPlayerIdentityResolver.ResolveUserId(User)` and then look up the seat from `GameSessionEntry.UidToSeatMap`.
- Return `403 Forbidden` if the authenticated user's UID is not found in the session's map (they are not a participant in that game).

### 9.5 Protect All Game Endpoints

Apply `[Authorize]` to all game controllers (or use a global authorization policy registered in `AddAuthorization`). Unauthenticated requests return `401 Unauthorized` before reaching any application logic.

### 9.6 Update Phase 8 Secret Keys

Add the following entry to the Expected Secret Keys table in `trashanimal_api_phase8_local_secrets.plan.md`:

| Key | Description | Added In |
|-----|-------------|----------|
| `Authentication:Firebase:ProjectId` | Firebase project ID used as JWT issuer and audience | Phase 9 |

## Constraints

- `GameApplicationService` must not reference any Firebase-specific type. All provider coupling is isolated to `ClaimsPrincipalPlayerIdentityResolver` and the JWT Bearer configuration in `ServiceCollectionExtensions`.
- `IGameSessionRepository` interface must remain unchanged by this phase.
- The `UidToSeatMap` must be treated as immutable after game creation; seats cannot be reassigned mid-game.
