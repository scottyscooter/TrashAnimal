# Developer Setup

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `dotnet` CLI (included with the SDK)

## Running Locally

```
dotnet run --project TrashAnimal.Api
```

The API starts with `ASPNETCORE_ENVIRONMENT=Development` by default, which enables user secrets automatically via `WebApplication.CreateBuilder`.

## Local Secrets (ASP.NET Core Secret Manager)

Credentials required during local development are managed by the [Secret Manager tool](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets). Secrets are stored outside the repository at:

```
%APPDATA%\Microsoft\UserSecrets\41448837-7d38-4d93-ad0c-2f5aa1557cab\secrets.json
```

You do **not** need to run `dotnet user-secrets init` — the `UserSecretsId` is already present in `TrashAnimal.Api.csproj`. Just use `set` directly.

This path is never inside the repository tree and cannot be accidentally committed.

### Setting a Secret

```
dotnet user-secrets set "<Key>" "<Value>" --project TrashAnimal.Api
```

### Listing All Secrets

```
dotnet user-secrets list --project TrashAnimal.Api
```

### Removing a Secret

```
dotnet user-secrets remove "<Key>" --project TrashAnimal.Api
```

## Required Secret Keys

| Key | Description | Added In |
|-----|-------------|----------|
| *(none yet)* | *(Phase 9 will add Firebase config keys)* | Phase 9 |

This table is the canonical reference. Add new keys here whenever a phase introduces them.

## Notes

- User secrets are loaded in the `Development` environment only. Production secrets will use a separate controlled store (Azure Key Vault or equivalent).
- Never commit secrets to source control. If you accidentally do, rotate the credential immediately.
