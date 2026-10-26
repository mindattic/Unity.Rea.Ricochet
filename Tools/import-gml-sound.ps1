<#
.SYNOPSIS
  Imports the original's sound effects (U.RICO-T50), music (U.RICO-T27) and announcers' voice lines (U.RICO-T51).
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

  The music_* sounds (U.RICO-T27) are mirrored the same way into Resources/Ricochet/Music/<sound>.<ext> and listed
  under "music" in the manifest (music_spacefighter names no recording and is skipped). Each gets a .meta, written only
  when missing, that sets the audio importer to Streaming so a track plays from disk instead of decompressing on load.
  The announcers' voice lines (U.RICO-T51): the seven personalities' sfx_<key>_{welcome,win,lose}1..10 (210 recordings)
  are mirrored into Resources/Ricochet/Voice/<sound>.ogg and listed under "voices", where HudOverlay has GameAudio play
  the one Core's Announcer drew when its card appears. Each takes the definition's recording's .ogg sibling (the same
  take, which the original's sound/audio holds beside every .wav): the .wavs are 162 MB that Unity would only re-encode
  to Vorbis on import, the .oggs 6 MB. Five definitions name no recording at all (sfx_sid_lose3/4, sfx_sly_lose4/5,
  sfx_vlad_lose4: the original played silence for those lines, a fixed defect); each is filled, as a .wav, from the
  voice actor's own take in -Takes (the project's sfx\announcers, whose takes are byte-identical to the numbered
  recordings wherever both exist) and the manifest says so ("importedFrom": "takes: ..."). The eighth set, sfx_announce_*, is listed under "skipped": no script or object of
  the original ever plays it (random_announcement_* and play_random_announcement call only the seven personalities').

  The 2013 copy (the one docs/RECONSTRUCTION.md was read from, and Tools/import-gml-art.ps1's default) is the default
  source. The 2018 re-save under GameMaker\gmlricochet\Ricochet.gmx has byte-identical recordings.

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File Tools/import-gml-sound.ps1
.EXAMPLE
  powershell -File Tools/import-gml-sound.ps1 -Source R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\GameMaker\gmlricochet\Ricochet.gmx
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am',
    # The voice actors' takes, one folder per personality (the project's sfx\announcers, beside Ricochet.gmx).
    [string]$Takes = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\GameMaker\gmlricochet\sfx\announcers'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
$inv = [Globalization.CultureInfo]::InvariantCulture

$root = Split-Path -Parent $PSScriptRoot
$soundAssets = 'Assets/Ricochet/Resources/Ricochet/Sound'
$soundDir = Join-Path $root $soundAssets
$musicAssets = 'Assets/Ricochet/Resources/Ricochet/Music'
$musicDir = Join-Path $root $musicAssets
$voiceAssets = 'Assets/Ricochet/Resources/Ricochet/Voice'
$voiceDir = Join-Path $root $voiceAssets
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

# --- the music (U.RICO-T27): streamed ----------------------------------------------------------------
function New-StreamingMeta([string]$assetPath) {
    $meta = "$assetPath.meta"
    if (Test-Path $meta) { return }
    $md5 = [Security.Cryptography.MD5]::Create()
    $guid = ([BitConverter]::ToString($md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("Ricochet/Music/" + (Split-Path -Leaf $assetPath)))) -replace '-', '').ToLowerInvariant()
    $text = @"
fileFormatVersion: 2
guid: $guid
AudioImporter:
  externalObjects: {}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 1
    quality: 0.7
    conversionMode: 0
    preloadAudioData: 0
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 1
  loadInBackground: 1
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 

"@
    [IO.File]::WriteAllText($meta, ($text -replace "`r`n", "`n"), (New-Object Text.UTF8Encoding $false))
}

$musicWritten = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$music = @()
foreach ($file in Get-ChildItem $sourceSound -Filter 'music_*.sound.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.sound\.gmx$', ''
    $s = (Read-Xml $file.FullName).sound
    $dataNode = $s.SelectSingleNode('data')
    $data = if ($dataNode -ne $null) { $dataNode.InnerText } else { '' }
    $recording = if ($data) { Join-Path (Join-Path $sourceSound 'audio') $data } else { $null }
    $ext = if ($data) { [IO.Path]::GetExtension($data).ToLowerInvariant() } else { '' }
    if (-not $data) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = 'the definition names no recording' }; continue }
    if (-not $ext -or -not (Test-Path $recording)) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = "its recording sound\audio\$data is missing" }; continue }

    $asset = "$musicAssets/$name$ext"
    $to = Join-Path $root $asset
    [void]$musicWritten.Add([IO.Path]::GetFullPath($to))
    $same = (Test-Path $to) -and ((Get-Item $recording).Length -eq (Get-Item $to).Length) -and ((Get-FileHash $recording).Hash -eq (Get-FileHash $to).Hash)
    if (-not $same) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $to) | Out-Null
        Copy-Item $recording $to -Force
        $copied++
    }
    New-StreamingMeta $to
    $music += [ordered]@{
        name = $name
        originalPath = "sound\$($file.Name)"
        originalRecording = "sound\audio\$data"
        bytes = (Get-Item $recording).Length
        volume = (Volume-Of $s)
        kind = [int]$s.SelectSingleNode('kind').InnerText
        streamed = ($s.SelectSingleNode('streamed') -ne $null -and $s.SelectSingleNode('streamed').InnerText -eq '-1' -or $s.SelectSingleNode('streamed') -ne $null -and $s.SelectSingleNode('streamed').InnerText -eq '1')
        asset = $asset
        resource = "Ricochet/Music/$name"
    }
}
if (Test-Path $musicDir) {
    foreach ($f in Get-ChildItem $musicDir -File | Where-Object { $_.Extension -ne '.meta' }) {
        if (-not $musicWritten.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
}

# --- the announcers' voice lines (U.RICO-T51): the .ogg sibling of each definition's recording -------
# The voice actor's take for sfx_<key>_<kind><n> in $Takes\<key>: <key>_<kind><n>.wav (sly, rye, ...) or
# <Anyone>-<Welcome|Win|Loss>-<n>.wav (sid's NN-Loss-3.wav, vlad's Vlad-Loss-4.wav). Byte-identical to the numbered
# recordings where both exist.
function Find-Take([string]$name) {
    if ($name -notmatch '^sfx_([a-z]+)_(welcome|win|lose)(\d+)$') { return $null }
    $key = $Matches[1]; $kind = $Matches[2]; $n = $Matches[3]
    $dir = Join-Path $Takes $key
    if (-not (Test-Path $dir)) { return $null }
    $plain = Join-Path $dir "$($key)_$kind$n.wav"
    if (Test-Path $plain) { return $plain }
    $word = @{ welcome = 'Welcome'; win = 'Win'; lose = 'Loss' }[$kind]
    $take = Get-ChildItem $dir -Filter "*-$word-$n.wav" | Select-Object -First 1
    if ($take -ne $null) { return $take.FullName }
    return $null
}

$voiceWritten = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$voices = @()
$unplayed = @()
foreach ($file in Get-ChildItem $sourceSound -Filter 'sfx_*.sound.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.sound\.gmx$', ''
    if ($name -notmatch $voice) { continue }
    if ($name -match '^sfx_announce_') { $unplayed += $name; continue }
    $s = (Read-Xml $file.FullName).sound
    $dataNode = $s.SelectSingleNode('data')
    $data = if ($dataNode -ne $null) { $dataNode.InnerText } else { '' }
    $ogg = if ($data) { [IO.Path]::ChangeExtension($data, '.ogg') } else { '' }
    $recording = if ($ogg) { Join-Path (Join-Path $sourceSound 'audio') $ogg } else { $null }
    $restored = $null
    if (-not $ogg) {
        # Five definitions name no recording (fixed defect): take the voice actor's own take from the project's
        # sfx\announcers folder, which holds every line under its recording-session name.
        $restored = Find-Take $name
        if ($restored -eq $null) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = "the definition names no recording and $Takes has no take for it" }; continue }
        $recording = $restored
    }
    elseif (-not (Test-Path $recording)) { $skipped += [ordered]@{ name = $name; originalPath = "sound\$($file.Name)"; reason = "its recording's Vorbis copy sound\audio\$ogg is missing" }; continue }

    $ext = [IO.Path]::GetExtension($recording).ToLowerInvariant()
    $asset = "$voiceAssets/$name$ext"
    $to = Join-Path $root $asset
    [void]$voiceWritten.Add([IO.Path]::GetFullPath($to))
    $same = (Test-Path $to) -and ((Get-Item $recording).Length -eq (Get-Item $to).Length) -and ((Get-FileHash $recording).Hash -eq (Get-FileHash $to).Hash)
    if (-not $same) {
        New-Item -ItemType Directory -Force (Split-Path -Parent $to) | Out-Null
        Copy-Item $recording $to -Force
        $copied++
    }
    $voices += [ordered]@{
        name = $name
        originalPath = "sound\$($file.Name)"
        originalRecording = if ($restored) { '' } else { "sound\audio\$data" }
        importedFrom = if ($restored) { 'takes: sfx\announcers\' + $restored.Substring($Takes.TrimEnd('\').Length + 1) } else { "sound\audio\$ogg" }
        bytes = (Get-Item $recording).Length
        volume = (Volume-Of $s)
        asset = $asset
        resource = "Ricochet/Voice/$name"
    }
}
if ($unplayed.Count -gt 0) {
    $skipped += [ordered]@{ name = 'sfx_announce_*'; originalPath = 'sound\sfx_announce_{welcome,win,lose}1..10.sound.gmx'; reason = "$($unplayed.Count) voice lines no script or object plays (random_announcement_* and play_random_announcement call only the seven personalities' random_<key>_* scripts)" }
}
if (Test-Path $voiceDir) {
    foreach ($f in Get-ChildItem $voiceDir -File | Where-Object { $_.Extension -ne '.meta' }) {
        if (-not $voiceWritten.Contains($f.FullName)) {
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
    sounds = $sounds
    music = $music
    voices = $voices
    skipped = $skipped
}
New-Item -ItemType Directory -Force $manifestDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

Write-Host "[Ricochet] GML sound: $($sounds.Count) recordings, $($music.Count) music tracks, $($voices.Count) voice lines, $($skipped.Count) skipped; $copied files copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($s in $skipped) { Write-Host "  skipped $($s.name): $($s.reason)" }
