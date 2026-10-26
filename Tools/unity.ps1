<#
.SYNOPSIS
  Runs Unity.Ricochet in Unity batch mode and surfaces compile/test errors -- the helper deploy.ps1 calls (same
  pattern as Unity.Hare and Unity.Chain's tools/unity.ps1).
.EXAMPLE
  powershell -File Tools/unity.ps1 -Log compile
  powershell -File Tools/unity.ps1 -Method Ricochet.Tooling.RicochetBuild.BuildWindows -Log deploy
  powershell -File Tools/unity.ps1 -Log test-playmode -Extra "-runTests -testPlatform PlayMode -testFilter Ricochet.Tests.SessionMuteAudioTests -testResults Logs/TestResults-PlayMode.xml"
.NOTES
  Unity batch mode needs the Editor closed on this project. This script refuses to run if it is open.
  Every run's full Unity log is captured to Logs/<Log>.log.
#>
param(
    [string]$Method,
    [string]$Log = "unity",
    [switch]$Graphics,
    [string]$Extra = ""
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = (Get-Content "$root/ProjectSettings/ProjectVersion.txt" | Select-String 'm_EditorVersion:').ToString().Split(':')[1].Trim()
$unity = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
if (-not (Test-Path $unity)) { throw "Unity $version not found at $unity" }

if (Test-Path "$root/Temp/UnityLockfile") {
    $locked = $false
    try { [IO.File]::Open("$root/Temp/UnityLockfile", 'Open', 'ReadWrite', 'None').Close() } catch { $locked = $true }
    if ($locked) { Write-Error "Unity.Ricochet is open in the Unity Editor. Close it (File > Quit) and re-run."; exit 3 }
}

New-Item -ItemType Directory -Force "$root/Logs" | Out-Null
$logFile = "$root/Logs/$Log.log"
# Start-Process joins these with bare spaces, so paths are quoted here or a space in one would split it.
$argList = @("-batchmode", "-projectPath", "`"$root`"", "-logFile", "`"$logFile`"")
if (-not $Graphics) { $argList += "-nographics" }
if ($Method) { $argList += @("-executeMethod", $Method) }
if ($Extra) { $argList += $Extra.Split(' ') }
# A -runTests run quits on its own once the suite finishes; an -executeMethod run needs -quit.
if ($Extra -notmatch 'runTests') { $argList += "-quit" }

$p = Start-Process -FilePath $unity -ArgumentList $argList -Wait -PassThru -NoNewWindow
Select-String -Path $logFile -Pattern 'error CS|\[Ricochet\]|\[Build\]|Shader error|Compilation failed' -ErrorAction SilentlyContinue |
    Select-Object -First 80 | ForEach-Object { Write-Host $_.Line }
Write-Host "exit=$($p.ExitCode)  log=$logFile"
exit $p.ExitCode
