# Silent-installs the embedded Sentinel.LogViewer MSI.
$ErrorActionPreference = 'Stop'

$toolsDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$installerPath = Join-Path $toolsDirectory 'Sentinel.LogViewer.msi'

$packageArguments = @{
    packageName    = $env:ChocolateyPackageName
    fileType       = 'msi'
    file           = $installerPath
    silentArgs     = '/qn /norestart'
    validExitCodes = @(0, 1641, 3010)
}

Install-ChocolateyInstallPackage @packageArguments
