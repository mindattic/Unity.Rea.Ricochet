<#
.SYNOPSIS
  Imports the original's sound effects (gmlRicochet's non-voice sfx_* recordings) into the game (U.RICO-T50).
.DESCRIPTION
  Reads every sound/sfx_*.sound.gmx except the announcers' voice lines (sfx_{announce,baxter,doctor,rye,scottish,sid,
  sly,vlad}_*, U.RICO-T51) and mirrors the recording each one names (its <data>, under sound/audio) into
  Assets/Ricochet/Resources/Ricochet/Sound/<sound>.<ext>, where GameAudio loads it by name (Core's SoundBook maps every
  SoundCue to that name). Files are copied only when their bytes changed; a recording the original no longer has is
  deleted.

  Assets/Ricochet/Audio/gml-sound.json records one entry per GML sound: its definition and recording (paths relative to
  the source folder), its GameMaker volume, kind and preload, and the Unity asset it becomes. Definitions without a
  recording are listed under "skipped" with the reason (the original's sfx_bounce_shotgun3 names no file, so a third
  of its shotgun pellets bounced in silence; see docs/BIBLE.md).

  The music_* sounds are U.RICO-T27's and the voice lines U.RICO-T51's; neither is imported here.

  The 2013 copy (the one docs/RECONSTRUCTION.md was read from, and Tools/import-gml-art.ps1's default) is the default
  source. The 2018 re-save under GameMaker\gmlricochet\Ricochet.gmx has byte-identical recordings.

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File Tools/import-gml-sound.ps1
.EXAMPLE
  powershell -File Tools/import-gml-sound.ps1 -Source R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\GameMaker\gmlricochet\Ricochet.gmx
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
$inv = [Globalization.CultureInfo]::InvariantCulture

$root = Split-Path -Parent $PSScriptRoot
$soundAssets = 'Assets/Ricochet/Resources/Ricochet/Sound'
$soundDir = Join-Path $root $soundAssets
$manifestDir = Join-Path $root 'Assets/Ricochet/Audio'
$manifestPath = Join-Path $manifestDir 'gml-sound.json'
$voice = '^sfx_(announce|baxter|doctor|rye|scottish|sid|sly|vlad)_'

$sourceSound = Join-Path $Source 'sound'
if (-not (Test-Path $sourceSound)) { throw "No sound folder under $Source" }

function Read-Xml([string]$path) {
    $doc = New-Object Xml.XmlDocument
    $doc.Load($path)
    return $doc
}

# GameMaker 8.1 writes <volume>1</volume>; Studio writes <volume><volume>1</volume></volume>.
function Volume-Of([Xml.XmlNode]$sound) {
    $node = $sound.SelectSingleNode('volume/volume')
    if ($node -eq $null) { $node = $sound.SelectSingleNode('volume') }
    if ($node -eq $null -or [string]::IsNullOrWhiteSpace($node.InnerText)) { return 1.0 }
    return [double]::Parse($node.InnerText, $inv)
}

$written = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$copied = 0
$sounds = @(); $skipped = @()
foreach ($file in Get-ChildItem $sourceSound -Filter 'sfx_*.sound.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.sound\.gmx$', ''
    if ($name -match $voice) { continue }
    $s = (Read-Xml $file.FullName).sound
    $dataNode = $s.SelectSingleNode('data')
    $data = if ($dataNode -ne $null) { $dataNode.InnerText } else { '' }
    $recording = if ($data) { Join-Path (Join-Path $sourceSound 'audio') $data } else { $null }
    $ext = if ($data) { [IO.Path]::GetExtension($data).ToLowerInvariant() } else { '' }
    if (-not $data) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = 'the definition names no recording' }; continue }
    if (-not $ext) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = "the definition's recording '$data' has no file extension and no such file exists" }; continue }
    if (-not (Test-Path $recording)) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = "its recording sound\audio\$data is missing" }; continue }

    $asset = "$soundAssets/$name$ext"
    $to = Join-Path $root $asset
    [void]$written.Add([IO.Path]::GetFullPath($to))
    $same = (Test-Path $to) -and ((Get-Item $recording).Length -eq (Get-Item $to).Length) -and ((Get-FileHash $recording).Hash -eq (Get-FileHash $to).Hash)
    if (-not $same) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $to) | Out-Null
        Copy-Item $recording $to -Force
        $copied++
    }

    $kindNode = $s.SelectSingleNode('kind')
    $preloadNode = $s.SelectSingleNode('preload')
    $sounds += [ordered]@{
        name = $name
        originalPath = "sound\$($file.Name)"
        originalRecording = "sound\audio\$data"
        bytes = (Get-Item $recording).Length
        volume = (Volume-Of $s)
        kind = if ($kindNode -ne $null) { [int]$kindNode.InnerText } else { 0 }
        preload = if ($preloadNode -ne $null) { [int]$preloadNode.InnerText } else { 0 }
        asset = $asset
        resource = "Ricochet/Sound/$name"
    }
}

# --- delete what the original no longer has ----------------------------------------------------------
$removed = 0
if (Test-Path $soundDir) {
    foreach ($f in Get-ChildItem $soundDir -File | Where-Object { $_.Extension -ne '.meta' }) {
        if (-not $written.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
}

# --- the manifest ------------------------------------------------------------------------------------
. (Join-Path $PSScriptRoot 'gml-json.ps1')
$manifest = [ordered]@{
    source = 'gmlRicochet 09-05-13_2.20am (GameMaker 8.1 / Studio project, "Beta 18"), read by Tools/import-gml-sound.ps1; paths below are relative to it'
    excluded = 'the announcers'' voice lines (sfx_{announce,baxter,doctor,rye,scottish,sid,sly,vlad}_*, U.RICO-T51) and the music_* tracks (U.RICO-T27)'
    sounds = $sounds
    skipped = $skipped
}
New-Item -ItemType Directory -Force $manifestDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

Write-Host "[Ricochet] GML sound: $($sounds.Count) recordings, $($skipped.Count) skipped; $copied files copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($s in $skipped) { Write-Host "  skipped $($s.name): $($s.reason)" }
