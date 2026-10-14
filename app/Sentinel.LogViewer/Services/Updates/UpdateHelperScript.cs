namespace Sentinel.LogViewer.Services.Updates;

/// <summary>
/// PowerShell script that installs a downloaded update after the application has exited
/// </summary>
internal static class UpdateHelperScript
{
	public const string Content = """
		param(
		    [Parameter(Mandatory = $true)][int] $ProcessId,
		    [Parameter(Mandatory = $true)][string] $Package,
		    [Parameter(Mandatory = $true)][string] $ApplicationDirectory,
		    [Parameter(Mandatory = $true)][string] $Executable
		)

		$ErrorActionPreference = 'Stop'
		$updateDirectory = Split-Path -Parent $Package
		Start-Transcript -Path (Join-Path $updateDirectory 'update.log') -Append | Out-Null
		try {
		    # The files of the running application cannot be replaced.
		    Wait-Process -Id $ProcessId -Timeout 60 -ErrorAction SilentlyContinue

		    if ($Package.EndsWith('.msi', [StringComparison]::OrdinalIgnoreCase)) {
		        $msiLog = Join-Path $updateDirectory 'msiexec.log'
		        $arguments = @('/i', "`"$Package`"", '/passive', '/norestart', '/l*v', "`"$msiLog`"")
		        $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru
		        if ($process.ExitCode -notin 0, 3010) {
		            throw "msiexec failed with exit code $($process.ExitCode)."
		        }
		    }
		    else {
		        $staging = Join-Path $updateDirectory 'extracted'
		        if (Test-Path -LiteralPath $staging) {
		            Remove-Item -LiteralPath $staging -Recurse -Force
		        }
		        Expand-Archive -LiteralPath $Package -DestinationPath $staging -Force

		        # Keep the user's settings and the portable marker.
		        robocopy $staging $ApplicationDirectory /E /XF appsettings.json NLogViewer.portable .portable /NJH /NJS /NP | Out-Null
		        if ($LASTEXITCODE -ge 8) {
		            throw "Copying the update failed (robocopy exit code $LASTEXITCODE)."
		        }
		    }
		}
		finally {
		    # Start the application again, updated or not.
		    if (Test-Path -LiteralPath $Executable) {
		        Start-Process -FilePath $Executable
		    }
		    Stop-Transcript | Out-Null
		}
		""";
}
