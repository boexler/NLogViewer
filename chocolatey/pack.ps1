#Requires -Version 5.1
<#
.SYNOPSIS
  Builds the Sentinel.LogViewer Chocolatey package from the official MSI.

.DESCRIPTION
  Stages install scripts, LICENSE.txt, VERIFICATION.txt (with SHA256), and a
  nuspec whose GitHub URLs are derived from the current repository so a rename
  does not require hand-editing metadata.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [Parameter(Mandatory = $true)]
    [string] $MsiPath,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,

    [string] $Repository = $env:GITHUB_REPOSITORY,

    [string] $ServerUrl = $(if ($env:GITHUB_SERVER_URL) { $env:GITHUB_SERVER_URL } else { 'https://github.com' }),

    [string] $DefaultBranch = $env:GITHUB_DEFAULT_BRANCH
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepositorySlug {
    <#
    .SYNOPSIS
      Resolves owner/name from Actions env, parameters, or the git origin remote.
    #>
    param([string] $RepositoryName)

    if (-not [string]::IsNullOrWhiteSpace($RepositoryName)) {
        return $RepositoryName.Trim()
    }

    $originUrl = git remote get-url origin
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originUrl)) {
        throw 'Repository was not provided and git remote origin is unavailable.'
    }

    if ($originUrl -match 'github\.com[:/](?<slug>[^/]+/[^/]+?)(?:\.git)?$') {
        return $Matches['slug']
    }

    throw "Could not parse GitHub repository from origin URL '$originUrl'."
}

function Get-DefaultBranchName {
    <#
    .SYNOPSIS
      Resolves the default branch from Actions, git, or a main fallback.
    #>
    param([string] $BranchName)

    if (-not [string]::IsNullOrWhiteSpace($BranchName)) {
        return $BranchName.Trim()
    }

    $originHead = git symbolic-ref refs/remotes/origin/HEAD 2>$null
    if ($originHead -match 'refs/remotes/origin/(.+)$') {
        return $Matches[1]
    }

    return 'main'
}

function ConvertFrom-Template {
    <#
    .SYNOPSIS
      Replaces __TOKEN__ placeholders in a template string.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string] $Template,

        [Parameter(Mandatory = $true)]
        [hashtable] $Tokens
    )

    $result = $Template
    foreach ($key in $Tokens.Keys) {
        $result = $result.Replace("__${key}__", [string]$Tokens[$key])
    }

    return $result
}

function Get-IconRef {
    <#
    .SYNOPSIS
      Picks a git ref that raw.githubusercontent.com can resolve for iconUrl.
    #>
    param([string] $BranchName)

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
        return $env:GITHUB_SHA.Trim()
    }

    $headSha = git rev-parse HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($headSha)) {
        return $headSha.Trim()
    }

    return $BranchName
}

$msiFile = Get-Item -LiteralPath $MsiPath
$repositorySlug = Get-RepositorySlug -RepositoryName $Repository
$branchName = Get-DefaultBranchName -BranchName $DefaultBranch
$repoUrl = "$($ServerUrl.TrimEnd('/'))/$repositorySlug"
$iconUrl = "https://raw.githubusercontent.com/$repositorySlug/$(Get-IconRef -BranchName $branchName)/chocolatey/icon.png"
$tokens = @{
    REPO_URL             = $repoUrl
    PACKAGE_SOURCE_URL   = "$repoUrl/tree/$branchName/chocolatey"
    BUG_TRACKER_URL      = "$repoUrl/issues"
    LICENSE_URL          = "$repoUrl/blob/$branchName/LICENSE.md"
    ICON_URL             = $iconUrl
    RELEASE_NOTES_URL    = "$repoUrl/releases"
    VERSION              = $Version
    SHA256               = (Get-FileHash -Algorithm SHA256 -LiteralPath $msiFile.FullName).Hash.ToLowerInvariant()
}

$chocolateyRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $chocolateyRoot
$stageRoot = Join-Path $chocolateyRoot 'obj/package'
$stageTools = Join-Path $stageRoot 'tools'

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stageTools | Out-Null
if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path (Get-Location).Path $OutputDirectory
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

Copy-Item -LiteralPath (Join-Path $chocolateyRoot 'tools/chocolateyInstall.ps1') -Destination $stageTools
Copy-Item -LiteralPath $msiFile.FullName -Destination (Join-Path $stageTools 'Sentinel.LogViewer.msi')
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.md') -Destination (Join-Path $stageTools 'LICENSE.txt')

$verificationTemplate = Get-Content -LiteralPath (Join-Path $chocolateyRoot 'VERIFICATION.txt.template') -Raw
[System.IO.File]::WriteAllText(
    (Join-Path $stageTools 'VERIFICATION.txt'),
    (ConvertFrom-Template -Template $verificationTemplate -Tokens $tokens)
)

$nuspecTemplate = Get-Content -LiteralPath (Join-Path $chocolateyRoot 'Sentinel.LogViewer.nuspec') -Raw
$stagedNuspec = Join-Path $stageRoot 'Sentinel.LogViewer.nuspec'
[System.IO.File]::WriteAllText(
    $stagedNuspec,
    (ConvertFrom-Template -Template $nuspecTemplate -Tokens $tokens)
)

& choco pack $stagedNuspec --version $Version --output-directory $OutputDirectory
if ($LASTEXITCODE -ne 0) {
    throw "choco pack failed with exit code $LASTEXITCODE."
}
