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
The development workflow therefore keeps SemVer 2 for NuGet packages and uses a flattened
prerelease label without dots for the Chocolatey package.

## Publishing

The `Publish Dev` GitHub Actions workflow always builds a prerelease of `Sentinel.LogViewer` and
exposes it as a workflow artifact. With `publish` enabled it also pushes that build to the same
destinations as a tagged release: GitHub Packages, nuget.org, the Chocolatey Community Repository,
and a GitHub prerelease.

Tagged releases build and publish a stable version of the same `Sentinel.LogViewer` package from
`release.yml`.
Pushes require repository Actions secrets named `NUGET_API_KEY` and `CHOCOLATEY_API_KEY`.
