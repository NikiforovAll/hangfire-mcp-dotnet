# Release prerequisites

One-time setup. Required before the first release; nothing here is touched per-release.

## 1. NuGet API key

1. Sign in at <https://www.nuget.org/> with the account that owns `Nall.Hangfire.Mcp`.
2. **Account → API Keys → Create**:
   - Key name: `hangfire-mcp-dotnet-ci`
   - Scopes: **Push** (and **Push new packages and package versions**)
   - Glob pattern: `Nall.Hangfire.Mcp*`
   - Expiration: 365 days (max)
3. Copy the key — it's shown once.

## 2. GitHub repository secret

`Settings → Secrets and variables → Actions → New repository secret`

- Name: `NUGET_API_KEY`
- Value: the key from step 1

## 3. GitHub Environment `NuGet`

`Settings → Environments → New environment` → name **`NuGet`**.

The `push-nuget` job is bound to this environment. Recommended protection rules:

- **Required reviewers**: yourself (publish requires explicit approval).
- **Deployment branches**: `Selected branches` → allow only `main` and tags `v*`.

The `NUGET_API_KEY` secret can live at repo level (step 2) or be moved into this environment for tighter scoping — the workflow reads it the same way.

## 4. Workflow permissions

`Settings → Actions → General → Workflow permissions`:

- **Read and write permissions** (Release Drafter needs to write release drafts).
- Tick **Allow GitHub Actions to create and approve pull requests**.

## 5. Branch protection on `main`

`Settings → Branches → Add rule` for `main`:

- Require pull request before merging.
- Require status checks **`Build-ubuntu-latest`** and **`Build-windows-latest`** to pass.
- Require linear history (Release Drafter relies on PR titles/labels).

## 6. PR labels

Release Drafter creates labels lazily, but pre-creating them keeps the picker tidy. From **Issues → Labels** add:

`breaking`, `feature`, `enhancement`, `bug`, `fix`, `chore`, `refactor`, `dependencies`, `documentation`.

Most are applied automatically by the autolabeler in `.github/release-drafter.yml`.

---

Once all six are done, follow [`releasing.md`](./releasing.md) for the per-release flow.
