# CD Pivot: GitHub Pages (UI) + Azure Container Apps (API)

## Context

The repo currently has an unfinished plan for self-hosting the API on a Raspberry Pi (`.claude/docs/plans/self-hosted on raspberry pi.md` + `raspberry pi docker api hosting.md`). The user has decided to go with Azure instead, prioritizing cost efficiency over the "shows I run infra at home" angle — this is a portfolio demo (friends playtesting occasionally, an occasional recruiter visit), not a production system needing scale/redundancy. The Pi docs will be deleted as stale/superseded.

**Decisions confirmed with the user:**
- API host: **Azure Container Apps** (Docker-based, scale-to-zero when idle — consumption-plan free grant of 180,000 vCPU-seconds / 360,000 GiB-seconds / 2M requests per month comfortably covers this traffic, so expected cost is **$0/month**). Rejected App Service: Free tier sleeps + has a hard 60 CPU-min/day cap; Basic tier avoids that but costs ~$13/mo even fully idle, with no scale-to-zero.
- Auth from GitHub Actions to Azure: **OIDC federated credentials** (no stored secrets/passwords — Azure trusts short-lived tokens issued to this specific repo). Full step-by-step setup included below since this is new to the user, including creating the Azure subscription itself.
- Azure infra defined as **infrastructure as code (Bicep)**, checked into the repo, rather than one-off imperative `az` commands.
- UI: GitHub Pages, as before — reuse the existing `deploy-ui.yml` design from the old Pi plan (still valid, unrelated to the API host change).
- No custom domain — default `*.azurecontainerapps.io` and `*.github.io` hostnames, both with free auto-HTTPS.
- Delete the two Raspberry Pi plan docs once this plan is written.

## Architecture

```
Users (friends, recruiters)
  -> GitHub Pages: https://<username>.github.io/TrashAnimal/
       -> REST + SignalR calls to:
  -> Azure Container App: https://trashanimal-api.<region>.azurecontainerapps.io
       -> TrashAnimal.Api (ASP.NET Core 10, in-memory sessions)
       -> scales to 0 replicas when idle, cold-starts (a few seconds) on first request
```

## Part 1 — One-time Azure account & subscription setup (manual, in browser)

1. Go to https://azure.microsoft.com/free/ and sign up (or sign in if already have a Microsoft account). Azure requires a credit card on file even for free-tier/consumption usage — it's only charged if usage exceeds the free grant, which this workload won't.
2. Once in the [Azure Portal](https://portal.azure.com), note your **Subscription ID** (Subscriptions blade) and **Tenant ID** (Microsoft Entra ID > Overview) — needed for the OIDC secrets in Part 3.
3. Optional but recommended: set up a **budget alert** (Cost Management + Billing > Budgets) at e.g. $5/month just as a tripwire, since this is a demo project and you don't want a surprise bill from a bug (e.g. a runaway loop hitting the API).

## Part 2 — Azure infrastructure as code (Bicep)

Rather than one-off `az` commands, the Container Apps environment + app are defined as a Bicep template checked into the repo, so the infra is versioned and reproducible.

### `infra/main.bicep` (new)
```bicep
@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Container image to deploy, e.g. ghcr.io/<owner>/trashanimal-api:<tag>')
param containerImage string

@description('GHCR username for registry pull auth')
param ghcrUsername string

@secure()
@description('GHCR PAT (read:packages scope) for registry pull auth')
param ghcrPassword string

var environmentName = 'trashanimal-env'
var appName = 'trashanimal-api'
var logAnalyticsName = 'trashanimal-logs'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource containerAppEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
      }
      registries: [
        {
          server: 'ghcr.io'
          username: ghcrUsername
          passwordSecretRef: 'ghcr-password'
        }
      ]
      secrets: [
        { name: 'ghcr-password', value: ghcrPassword }
      ]
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}

output fqdn string = containerApp.properties.configuration.ingress.fqdn
```

Notes:
- `minReplicas: 0` is the scale-to-zero setting that keeps this free; `maxReplicas: 1` matches the "no need to scale" requirement.
- `targetPort: 8080` matches `ASPNETCORE_URLS` in the Dockerfile below.
- `cpu: 0.25` / `memory: 0.5Gi` is the smallest allowed size.
- GHCR credentials are passed as deployment parameters (from GitHub secrets, see Part 4), not hardcoded — avoids committing a PAT to the repo.
- Log Analytics is required by Container Apps environments for logging; `PerGB2018` is pay-as-you-go with a free 5GB/month allotment, effectively $0 at this log volume.

### One-time bootstrap (Azure CLI, run locally once)

Install the Azure CLI (`winget install Microsoft.AzureCLI` on Windows), run `az login`, then create just the resource group (everything else is defined by the Bicep template and deployed by GitHub Actions in Part 4):
```bash
az group create --name trashanimal-rg --location eastus
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.OperationalInsights
```

## Part 3 — OIDC federated credential setup (Azure CLI, one-time)

This lets GitHub Actions authenticate to Azure with no stored password/secret.

```bash
# Create an app registration + service principal
az ad app create --display-name "trashanimal-github-actions" 
# Note the "appId" from the output -> this is AZURE_CLIENT_ID
APP_ID="<appId from above>"
az ad sp create --id $APP_ID

# Grant Contributor on just the resource group (least privilege — not the whole subscription)
SUBSCRIPTION_ID="<your subscription id>"
az role assignment create \
  --assignee $APP_ID \
  --role Contributor \
  --scope /subscriptions/$SUBSCRIPTION_ID/resourceGroups/trashanimal-rg

# Register a federated credential trusting GitHub Actions runs from this repo's main branch
az ad app federated-credential create \
  --id $APP_ID \
  --parameters '{
    "name": "trashanimal-main-branch",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:<your-github-username>/TrashAnimal:ref:refs/heads/main",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

Then in the GitHub repo (Settings > Secrets and variables > Actions), add three **repository secrets**:
- `AZURE_CLIENT_ID` = the `appId` from above
- `AZURE_TENANT_ID` = your tenant ID
- `AZURE_SUBSCRIPTION_ID` = your subscription ID

None of these are secret credentials by themselves (no password/cert) — OIDC means the workflow exchanges GitHub's own short-lived token for an Azure token at runtime, scoped only to this repo/branch.

## Part 4 — Files to add/modify in the repo

### `TrashAnimal.Api/Dockerfile` (new)
Multi-stage build, adapted from the old Pi plan but targeting linux/amd64 (Container Apps' default, no QEMU cross-compile needed) and port 8080:
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TrashAnimal/TrashAnimal.csproj TrashAnimal/
COPY TrashAnimal.Api/TrashAnimal.Api.csproj TrashAnimal.Api/
RUN dotnet restore TrashAnimal.Api/TrashAnimal.Api.csproj
COPY TrashAnimal/ TrashAnimal/
COPY TrashAnimal.Api/ TrashAnimal.Api/
RUN dotnet publish TrashAnimal.Api/TrashAnimal.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrashAnimal.Api.dll"]
```
Build context must be the repo root (so `COPY TrashAnimal/` can reach the `ProjectReference`d domain project) — same reasoning as the old Pi plan.

### `.dockerignore` (new, repo root)
Excludes `**/bin`, `**/obj`, `TrashAnimal.Web/node_modules`, `TrashAnimal.Web/dist`, `.git`.

### `.github/workflows/deploy-api.yml` (new)
- Trigger: push to `main`, path-filtered to `TrashAnimal/**`, `TrashAnimal.Api/**`, `TrashAnimal.Api/Dockerfile`, `.github/workflows/deploy-api.yml`.
- Permissions: `id-token: write` (for OIDC), `contents: read`, `packages: write` (for GHCR push).
- Steps:
  1. `actions/checkout@v6`
  2. `docker/login-action@v3` against `ghcr.io` using `secrets.GITHUB_TOKEN`
  3. `docker/build-push-action@v6` — context `.`, file `TrashAnimal.Api/Dockerfile`, `platforms: linux/amd64`, push `true`, tags `ghcr.io/<owner>/trashanimal-api:latest` and `:${{ github.sha }}`
  4. `azure/login@v2` using `client-id`/`tenant-id`/`subscription-id` from the three repo secrets (OIDC — no `creds`/secret needed)
  5. `az deployment group create --resource-group trashanimal-rg --mode Incremental --template-file infra/main.bicep --parameters containerImage=ghcr.io/<owner>/trashanimal-api:${{ github.sha }} ghcrUsername=${{ github.actor }} ghcrPassword=${{ secrets.GHCR_PULL_PAT }}` — this step runs on **every push to `main`**, not just the first deploy. Despite the verb "create", `az deployment group create` is Azure's standard command for every Bicep/ARM deployment (there is no separate "update" command) — it diffs the template against what already exists in the resource group and applies only the changes (e.g. swapping the container image tag), rather than failing or recreating resources that are already there. `--mode Incremental` (Azure's default, made explicit here) guarantees it only adds/updates resources declared in the template and never deletes out-of-band resources in the group. First run stands up the Log Analytics workspace + Container Apps environment + app from scratch; every run after that just updates the running Container App in place — this is the continuous-deployment mechanism, not a one-time bootstrap. Using the sha tag (not `:latest`) gives an explicit, immutable rollback target — to roll back, re-run this same deployment step with an older `containerImage` tag (e.g. via a manual `workflow_dispatch` input, or by re-running a prior successful workflow run).

### GHCR pull credential (new GitHub secret)
Since the GHCR package will be private, generate a GitHub Personal Access Token (classic) with `read:packages` scope only, and store it as repo secret `GHCR_PULL_PAT` — this is what the Bicep template's `ghcrPassword` parameter receives.
(Alternative: make the GHCR package public under repo package settings, which avoids this secret entirely — reasonable for a portfolio demo since the image contains no secrets, only compiled app code. If chosen, drop the `registries`/`secrets` blocks from the Bicep template and the `ghcrUsername`/`ghcrPassword` params.)

### `.github/workflows/deploy-ui.yml` (new)
Same design as the old Pi plan's version — GitHub Pages deploy is unaffected by the API host change:
```yaml
name: Deploy UI to GitHub Pages
on:
  push:
    branches: [main]
    paths:
      - 'TrashAnimal.Web/**'
      - '.github/workflows/deploy-ui.yml'
permissions:
  contents: read
  pages: write
  id-token: write
jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: 'npm'
          cache-dependency-path: 'TrashAnimal.Web/package-lock.json'
      - run: npm ci
        working-directory: TrashAnimal.Web
      - run: npm run build
        working-directory: TrashAnimal.Web
        env:
          VITE_API_BASE_URL: https://trashanimal-api.<region>.azurecontainerapps.io
      - uses: peaceiris/actions-gh-pages@v4
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: ./TrashAnimal.Web/dist
```
Enable Pages in repo Settings > Pages > Source: "GitHub Actions" (one-time, manual).

### `TrashAnimal.Web/vite.config.ts` (update)
GitHub Pages serves a project site at `/TrashAnimal/`, so add:
```typescript
export default defineConfig({
  plugins: [react(), tailwindcss()],
  base: '/TrashAnimal/',
})
```

### `TrashAnimal.Api/appsettings.json` (update) + `appsettings.Production.json` (new)
Add the GitHub Pages origin to CORS, and reduce logging verbosity in production:
```json
// appsettings.json — add to CorsOptions.AllowedOrigins
"https://<your-github-username>.github.io"
```
```json
// appsettings.Production.json (new)
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "CorsOptions": { "AllowedOrigins": [ "https://<your-github-username>.github.io" ] }
}
```

### `.github/workflows/dotnet.yml` (no change needed)
Existing CI build/test workflow is independent of deployment and keeps running as-is on push/PR to `main`.

## Part 5 — Delete stale plan docs
Delete `.claude/docs/plans/self-hosted on raspberry pi.md` and `.claude/docs/plans/raspberry pi docker api hosting.md` (superseded by this plan).

## Cost Summary

| Component | Service | Monthly cost |
|---|---|---|
| UI | GitHub Pages | $0 |
| API | Azure Container Apps (consumption, scale-to-zero) | $0 (within free grant for this traffic) |
| Container registry | GHCR | $0 |
| **Total** | | **$0/month** (barring a traffic spike far beyond expected use) |

## Verification

1. Run Part 1–3 manually (Azure signup, resource creation, OIDC setup) and confirm the three GitHub secrets are set.
2. Push a trivial change under `TrashAnimal.Api/` to `main`; confirm `deploy-api.yml` runs green end-to-end (build → push to GHCR → `az deployment group create` applying `infra/main.bicep`).
3. `curl https://trashanimal-api.<region>.azurecontainerapps.io/games` (or a lightweight GET) returns a valid response — confirm cold start from 0 replicas works (first request may take a few seconds).
4. Push a trivial change under `TrashAnimal.Web/` to `main`; confirm `deploy-ui.yml` runs green and the site is live at `https://<username>.github.io/TrashAnimal/`.
5. Open the GitHub Pages URL, create a game, verify REST calls and the SignalR connection succeed against the Container App (check browser console/network tab for CORS or connection errors).
6. Check the Azure Portal's Container App > Metrics after a day of no traffic to confirm it scaled to 0 replicas (no ongoing cost).
7. Deliberately push a bad image (e.g., failing to bind port 8080) to confirm re-running the Bicep deployment with a prior good `containerImage` tag rolls back cleanly.
