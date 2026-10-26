<#
.SYNOPSIS
    Builds the standalone Windows player for development to Build/Ricochet/Ricochet.exe.
.DESCRIPTION
    Runs Tools/unity.ps1 with RicochetBuild.BuildWindows (Unity batch mode; the Editor must be closed). This is a
    bare dev build -- no icon, product-settings polish or release checklist -- good enough for Tools/play.ps1 to
    launch. The Unity log goes to Logs/deploy.log. (Hosting a dedicated server is mp-host's job, not this script's.)
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $root 'Build/Ricochet/Ricochet.exe'
if (Test-Path $exe) { Remove-Item $exe -Force }

& (Join-Path $PSScriptRoot 'unity.ps1') -Method Ricochet.Tooling.RicochetBuild.BuildWindows -Log deploy
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) { exit $exitCode }
if (-not (Test-Path $exe)) {
    Write-Error "Unity exited 0 but $exe was not produced; see Logs/deploy.log"
    exit 1
}
Write-Host "Built $exe"
exit 0
