---
name: trashanimal_api_phase8_local_secrets
overview: Wire the ASP.NET Core Secret Manager into TrashAnimal.Api so credentials required during local development are never stored in source control.
todos:
  - id: init-user-secrets
    content: Run dotnet user-secrets init on TrashAnimal.Api to add UserSecretsId to the .csproj
    status: completed
  - id: document-secret-keys
    content: Add an Expected Secret Keys section to the plan listing all keys developers must set locally
    status: completed
  - id: developer-setup-docs
    content: Add a DEVELOPER_SETUP.md (or README section) explaining the dotnet user-secrets workflow for new contributors
    status: completed
isProject: false
---

# Phase 8: Local Development Secrets

## Goal

Prevent credentials from appearing in source control by wiring the ASP.NET Core Secret Manager into `TrashAnimal.Api`. This phase establishes the mechanism; Phase 9 (auth) will populate the first real secret keys.

## Tasks

### 8.1 Initialize User Secrets

Run the following command from the `TrashAnimal.Api` project directory:

```
dotnet user-secrets init
```

This adds a `<UserSecretsId>` element (a GUID) to `TrashAnimal.Api.csproj`:

```xml
<PropertyGroup>
  <UserSecretsId><!-- generated GUID --></UserSecretsId>
</PropertyGroup>
```

No application code change is required. `WebApplication.CreateBuilder` automatically loads user secrets from `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` when `ASPNETCORE_ENVIRONMENT` is `Development`.

### 8.2 Document Expected Secret Keys

Maintain a canonical list of keys that must be present for the project to run locally. Initially empty; Phase 9 will add Firebase keys. The list lives in this plan and in `DEVELOPER_SETUP.md`.

## Expected Secret Keys

| Key | Description | Added In |
|-----|-------------|----------|
| *(none yet)* | *(Phase 9 will add Firebase config keys)* | Phase 9 |

Developers set each key with:

```
dotnet user-secrets set "<Key>" "<Value>" --project TrashAnimal.Api
```

### 8.3 Developer Setup Documentation

Create `DEVELOPER_SETUP.md` at the repository root covering:

- Prerequisites (SDK version, dotnet CLI)
- How to initialize user secrets if `UserSecretsId` is already in the `.csproj` (no `init` needed; just `set`)
- The full list of required keys (mirrors the table above)
- How to verify secrets are loaded (`dotnet user-secrets list --project TrashAnimal.Api`)
- A note that secrets are stored at `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` and are never committed to git

## Constraints

- User secrets are loaded in the `Development` environment only. Do not call `AddUserSecrets` explicitly — rely on the automatic behavior of `WebApplication.CreateBuilder`.
- Production secrets will use a controlled store (Azure Key Vault or equivalent); this phase does not configure that.
- The `secrets.json` file path is outside the repository tree and cannot be accidentally committed.
