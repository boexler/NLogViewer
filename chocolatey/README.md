# Chocolatey package

Stable releases and development builds use the same `Sentinel.LogViewer` package ID. The package
version selects the channel:

- Stable releases use versions such as `1.4.2`.
- Development builds use prerelease versions such as `1.4.3-dev00000042001`.

Chocolatey ignores prereleases by default. Use `--pre` to opt into development builds:

```powershell
choco install Sentinel.LogViewer -y
choco upgrade Sentinel.LogViewer -y
choco upgrade Sentinel.LogViewer --pre -y
```

The Chocolatey CLI supports SemVer 2, but the Chocolatey Community Repository currently does not.

**Do not** put GitVersion `fullSemVer` or SemVer 2 labels such as `3.2.0-dev.4.1` on the Chocolatey package. MSI, portable ZIPs, and NuGet packages always use `fullSemVer`. Only this package flattens development builds to `major.minor.patch-dev{runNumber:D10}{runAttempt:D3}` (for example `3.2.0-dev0000000004001`). Tagged releases pack Chocolatey with the same stable `fullSemVer` as the MSI (`3.2.0`).

## Packaging

`chocolatey/pack.ps1` is the only supported way to build the nupkg. It copies
`LICENSE.md` to `LICENSE.txt`, writes `VERIFICATION.txt` with the MSI SHA256,
and fills GitHub URLs from the current repository (`GITHUB_REPOSITORY` in CI, or
`git remote origin` locally). Do not run `choco pack` on the nuspec template
directly; it still contains placeholders.

```powershell
./chocolatey/pack.ps1 `
  -Version 3.2.0 `
  -MsiPath path/to/Sentinel.LogViewer.msi `
  -OutputDirectory chocolatey-packages
```

## Publishing

The `Publish Dev` GitHub Actions workflow always builds a prerelease of `Sentinel.LogViewer` and
exposes it as a workflow artifact. With `publish` enabled it also pushes that build to the same
destinations as a tagged release: GitHub Packages, nuget.org, the Chocolatey Community Repository,
and a GitHub prerelease.

Tagged releases build and publish a stable version of the same `Sentinel.LogViewer` package from
`release.yml`.
Pushes require repository Actions secrets named `NUGET_API_KEY` and `CHOCOLATEY_API_KEY`.
