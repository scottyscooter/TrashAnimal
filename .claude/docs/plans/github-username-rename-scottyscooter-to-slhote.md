# GitHub Username Rename: scottyscooter → slhote

## Context

The GitHub account that owns this repo was renamed from `scottyscooter` to `slhote`. The prior Azure Container Apps + GitHub Pages CD setup (implemented and merged across PRs #37-#42) has the old username baked in a few places — some as literal strings that need editing, some as external Azure/GitHub configuration that isn't in the repo at all and needs manual updates. Goal: enumerate every place affected so nothing silently breaks (in particular, Azure OIDC auth, which will hard-fail deploys if missed).

I audited the repo with a full-text search for `scottyscooter` and `github.io`/`github.com` — most of the CD pipeline was deliberately built with dynamic GitHub Actions expressions (`${{ github.repository_owner }}`, `${{ github.actor }}`) specifically so it wouldn't hardcode the owner, which pays off here: only 2 files in the repo have a literal hardcoded reference. Everything else that needs attention lives outside the repo (Azure AD, GHCR, git remotes, browser bookmarks).

## What needs to change

### 1. Repo code (2 files, straightforward edits)
- [TrashAnimal.Api/appsettings.json:6](TrashAnimal.Api/appsettings.json:6) — `CorsOptions.AllowedOrigins` hardcodes `"https://scottyscooter.github.io"` → `"https://slhote.github.io"`
- [TrashAnimal.Api/appsettings.Production.json:9](TrashAnimal.Api/appsettings.Production.json:9) — same `AllowedOrigins` value, same fix
- These must change together — the old origin will no longer be the real GitHub Pages domain once the rename propagates, and CORS will start rejecting the frontend's requests otherwise.

### 2. Azure OIDC federated credential — **critical, will break deploys if missed**
- The federated credential created in Part 3 of the original plan has a literal `subject` claim: `repo:scottyscooter/TrashAnimal:ref:refs/heads/main`.
- This is **not** dynamic — GitHub's OIDC token's `sub` claim reflects the *current* repo owner at workflow run time, so once the rename takes effect it will present `repo:slhote/TrashAnimal:ref:refs/heads/main`, which no longer matches the credential Azure has on file. `azure/login@v2` will fail on the next push to `main`.
- Fix (one-time, manual, via `az ad app federated-credential update` or delete-and-recreate with the corrected `subject`): update the federated credential registered on the `trashanimal-github-actions` app registration in Microsoft Entra ID.
- No repo secret needs to change (`AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` are all identity/tenant/subscription IDs, unrelated to the GitHub username) — only the federated credential's subject string.

### 3. GHCR (GitHub Container Registry) image path
- `deploy-api.yml` already builds/pushes/pulls via `ghcr.io/${{ github.repository_owner }}/trashanimal-api` and passes `ghcrUsername: ${{ github.actor }}` into the Bicep deployment — both dynamic, so the *workflow* will automatically target `ghcr.io/slhote/trashanimal-api` on the next run without editing.
- Caveat worth verifying, not just assuming: the currently-published image sits at `ghcr.io/scottyscooter/trashanimal-api`. GitHub Packages/GHCR namespace behavior on an account rename isn't something I can confirm with full confidence from here — watch the first post-rename `deploy-api.yml` run closely (push, pull-auth, and the Bicep `containerApps` registry config all need to agree on the new path) rather than assuming it "just works."
- The `GHCR_PULL_PAT` secret itself keeps working after a rename (PATs are tied to the account, not the username string) — no action needed there.

### 4. Deployed Azure Container App's current registry config
- The live Container App's `registries`/`secrets` block (from `infra/main.bicep`) was populated with the **old** `ghcrUsername=scottyscooter` on its last deployment. This self-heals automatically on the next successful `deploy-api.yml` run (the Bicep `az deployment group create` step re-applies `ghcrUsername=${{ github.actor }}`, now resolving to `slhote`) — but only once fix #2 (OIDC) is in place, since the deploy can't even authenticate until then.
- No resource names change — `trashanimal-rg`, `trashanimal-env`, `trashanimal-api`, `trashanimal-logs` are all username-independent.

### 5. Git remote (local, cosmetic/convenience)
- `git remote -v` currently shows `https://github.com/scottyscooter/TrashAnimal.git`. GitHub redirects the old URL for pushes/fetches for a grace period after a rename, but it's cleaner to update: `git remote set-url origin https://github.com/slhote/TrashAnimal.git`.

### 6. GitHub Pages URL / live links (no config, just consequence)
- The site's real URL becomes `https://slhote.github.io/TrashAnimal/` automatically — this isn't a setting anywhere, it's inherent to the account name. No repo change needed beyond #1's CORS update.
- Any previously-shared links (e.g. a `ShareLink` URL a friend already has, pointing at `https://scottyscooter.github.io/...`) will break with no way to fix retroactively — worth a heads-up to anyone you'd shared a link with, not a code fix.

### 7. Things checked and confirmed unaffected (no action needed)
- `infra/main.bicep` — no hardcoded username anywhere (`ghcrUsername` is a parameter, supplied at deploy time)
- `deploy-api.yml` / `deploy-ui.yml` — no hardcoded username; both already use `github.repository_owner`/`github.actor`
- `TrashAnimal.Web/vite.config.ts`'s `base: '/TrashAnimal/'` — keyed to the **repo name**, not the username; unaffected by an account rename
- `ShareLink.tsx` — builds its URL from `window.location.origin` dynamically; will automatically reflect the new domain once Pages migrates, no code change
- `.claude/docs/plans/azure-github-pages-cd.md` — written with generic `<your-github-username>`/`<owner>` placeholders throughout, not the literal string; no update needed
- `DEVELOPER_SETUP.md` and all `CLAUDE.md` files — no username references found

## Suggested order of operations
1. Update the Azure federated credential's `subject` first (nothing else works until this is right)
2. Edit the two `appsettings*.json` CORS origins in the repo, commit via a branch + PR (per your standing convention: push and open PR, don't merge without explicit go-ahead)
3. Update the local git remote URL
4. Merge the PR, watch `deploy-api.yml` run — confirm it authenticates (validates #1) and check the GHCR/registry path resolves correctly (validates #3/#4)
5. Watch `deploy-ui.yml` run, then load `https://slhote.github.io/TrashAnimal/` and confirm CORS/API connectivity works (validates #1's CORS fix)

## Verification
- `az ad app federated-credential list --id <APP_ID>` shows the corrected `subject`
- `deploy-api.yml` run succeeds end-to-end post-rename (build → push → `az deployment group create`)
- `deploy-ui.yml` run succeeds; site loads at the new domain
- Create a lobby on the live site, confirm no CORS errors in the browser console, confirm the SignalR WebSocket connects
- Copy a fresh `ShareLink` and confirm it points at `https://slhote.github.io/TrashAnimal/...`
