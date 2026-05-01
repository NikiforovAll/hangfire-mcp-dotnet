# Release process

Tag-driven. MinVer derives the package version from git tags; Release Drafter assembles the notes.

> **First time?** Complete [`release-prerequisites.md`](./release-prerequisites.md) before running through this guide.

## How versions are computed

[MinVer](https://github.com/adamralph/minver) reads the latest reachable git tag of the form `vX.Y.Z`.

- On a commit that **is** a tag → version is `X.Y.Z`.
- On a commit **after** the latest tag → version is `X.Y.(Z+1)-preview.0.N` (preview build).
- No tags yet → `0.0.0-preview.0.N`.

CI build numbers (`MINVERBUILDMETADATA=build.<run_number>`) are appended as build metadata.

## Labels drive the version bump

Release Drafter computes the next version from PR labels merged since the previous tag:

| Label                                                           | Bump  |
| --------------------------------------------------------------- | ----- |
| `breaking`, `major`                                             | major |
| `feature`, `enhancement`, `minor`                               | minor |
| `bug`, `fix`, `chore`, `documentation`, `dependencies`, `patch` | patch |

PRs are auto-labeled by title (`feat:` / `fix:` / `chore:` / `refactor:` / `!:` for breaking) and by changed files (`*.md` → `documentation`, `*.csproj` / `global.json` → `dependencies`).

## Stable release

1. Merge PRs into `main`. Release Drafter keeps a draft release at **Releases → Draft** with categorized notes and the calculated `vX.Y.Z`.
2. Open the draft, review notes, adjust the tag if needed, click **Publish release**.
3. Publishing creates the git tag and fires `release: published`.
4. The `Build` workflow then:
   - packs `Nall.Hangfire.Mcp` and `Nall.Hangfire.Mcp.Generator`,
   - pushes both `.nupkg` files to nuget.org via the `NuGet` environment.

## Prerelease

Same flow — only the tag changes. MinVer stamps whatever SemVer is in the tag, and nuget.org auto-classifies anything with a `-suffix` as a prerelease.

1. On the draft release page, change the tag from `v1.2.0` to `v1.2.0-alpha.1` (or `-beta.1`, `-rc.1`).
2. Tick **Set as a pre-release**.
3. **Publish release** → CI packs `Nall.Hangfire.Mcp.1.2.0-alpha.1.nupkg` and pushes it.

Conventions: `-alpha.N` (early), `-beta.N` (feature-complete), `-rc.N` (release candidate). Successive tags work fine — SemVer ordering keeps `*-*` ranges pointing at the newest.

Install:

```pwsh
dotnet add package Nall.Hangfire.Mcp --prerelease
# or pin:
dotnet add package Nall.Hangfire.Mcp --version 1.2.0-alpha.1
```

### Untagged preview builds

Commits on `main` produce a preview version like `1.2.0-preview.0.42` in CI artifacts. These are **not pushed** to nuget.org — download the `.nupkg` from the workflow run's artifacts if you need them.

## Manual / dry-run pack

Run the **Build** workflow via `workflow_dispatch`. It packs and uploads artifacts but does **not** push to nuget.org (push runs only on `release: published`).

## Local pack

```pwsh
dotnet pack src/Nall.Hangfire.Mcp/Nall.Hangfire.Mcp.csproj --configuration Release -o ./Artefacts
dotnet pack src/Nall.Hangfire.Mcp.Generator/Nall.Hangfire.Mcp.Generator.csproj --configuration Release -o ./Artefacts
```

Override the version without tagging:

```pwsh
dotnet pack src/Nall.Hangfire.Mcp/Nall.Hangfire.Mcp.csproj -c Release -o ./Artefacts -p:MinVerVersionOverride=1.2.3
```
