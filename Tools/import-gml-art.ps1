<#
.SYNOPSIS
  Imports the original's art (gmlRicochet's sprites and backgrounds) into Assets/Rea.Ricochet/Art (U.RICO-T44).
.DESCRIPTION
  Reads Ricochet.project.gmx (the resource tree, so every sprite keeps its GameMaker group), every
  sprites/*.sprite.gmx (frames, origin, bounding box, collision-mask shape) and background/*.background.gmx (image,
  tiling), and the image_speed each sprite's objects set in their Create event.

  Every resource on disk is imported, including the ones the project file no longer lists (left_border,
  spr_spritefont, spr_player, ...): each entry says whether it is in the resource tree ("inProject"), and the ones
  that are not go under the "Unlisted" group. Nothing the original shipped is lost, and the game only ever asks the
  catalogue for the resources it draws.

  The frame images are mirrored into Assets/Rea.Ricochet/Art/Sprites/<group>/<sprite>/<sprite>_<frame>.png and the
  backgrounds into Assets/Rea.Ricochet/Art/Backgrounds/<group>/<background>.png (copying only files whose bytes changed,
  deleting any the original no longer has), and Assets/Rea.Ricochet/Art/gml-art.json records one entry per GML sprite and
  background: its source data, the original files it came from (relative to the source folder) and the Unity assets
  it becomes.

  Unity then does the rest from that file: the editor's GmlArtImporter sets each texture's import settings (pivot on
  the GML origin, pixels per unit, filtering, tiling) and GmlArtBuilder writes the sprite atlases and the runtime
  ArtCatalogue (Resources/Ricochet/ArtCatalogue). Unless -NoUnity is given this script runs GmlArtBuilder headless
  through Tools/unity.ps1 once the files are in place.

  The 2013 copy (the one docs/RECONSTRUCTION.md was read from) is the default source. The 2018 re-save under
  GameMaker\gmlricochet\Ricochet.gmx has byte-identical images; only its .gmx XML differs (GameMaker Studio's later
  fields and glyph order).

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File Tools/import-gml-art.ps1
.EXAMPLE
  powershell -File Tools/import-gml-art.ps1 -Source D:\copy\of\gmlRicochet -NoUnity
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am',
    [switch]$NoUnity
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
$inv = [Globalization.CultureInfo]::InvariantCulture

$root = Split-Path -Parent $PSScriptRoot
$artAssets = 'Assets/Rea.Ricochet/Art'
$artDir = Join-Path $root $artAssets
$manifestPath = Join-Path $artDir 'gml-art.json'

# Unity-side constants the manifest records so the editor reads them from data, not code.
$pixelsPerUnit = 1920.0 / 22.0  # the original's 1920 px room is ShapeLibrary's 22 units wide
$roomSpeed = 60                 # normalFPS (globals.gml): every animation plays at it
$atlasMaxSide = 1024            # a sprite wider or taller than this keeps its own texture
$atlasPrefix = 'Sprites_'
$unlisted = 'Unlisted'

$projectFile = Join-Path $Source 'Ricochet.project.gmx'
if (-not (Test-Path $projectFile)) { throw "No Ricochet.project.gmx under $Source" }

function Read-Xml([string]$path) {
    $doc = New-Object Xml.XmlDocument
    $doc.Load($path)
    return $doc
}

function Png-Size([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $b = New-Object byte[] 24
        if ($fs.Read($b, 0, 24) -ne 24) { return $null }
        if ($b[0] -ne 0x89 -or $b[1] -ne 0x50 -or $b[12] -ne 0x49 -or $b[13] -ne 0x48) { return $null }
        $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
        $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
        return @($w, $h)
    } finally { $fs.Dispose() }
}

# --- the resource tree: every listed sprite and background with its GameMaker group ---------------------
$project = Read-Xml $projectFile
function Group-Of([Xml.XmlNode]$leaf, [string]$container) {
    $parts = @()
    $n = $leaf.ParentNode
    while ($n -ne $null -and $n.LocalName -eq $container) {
        $parts = @($n.GetAttribute('name')) + $parts
        $n = $n.ParentNode
    }
    # The top folder is GameMaker's own ("sprites", "background"); the groups under it are the author's.
    if ($parts.Count -gt 0) { $parts = @($parts | Select-Object -Skip 1) }
    return ($parts -join '/')
}
$spriteGroup = @{}
foreach ($node in $project.SelectNodes('//sprites//sprite')) { $spriteGroup[($node.InnerText -replace '^sprites\\', '')] = (Group-Of $node 'sprites') }
$backgroundGroup = @{}
foreach ($node in $project.SelectNodes('//backgrounds//background')) { $backgroundGroup[($node.InnerText -replace '^background\\', '')] = (Group-Of $node 'backgrounds') }

# --- which objects show each sprite, and the image_speed their Create sets -------------------------------
$objectSprite = @{}; $objectCreateSpeed = @{}
foreach ($file in Get-ChildItem (Join-Path $Source 'objects') -Filter '*.object.gmx') {
    $name = $file.Name -replace '\.object\.gmx$', ''
    $doc = Read-Xml $file.FullName
    $objectSprite[$name] = [string]$doc.object.spriteName
    $create = $doc.SelectSingleNode("//event[@eventtype='0' and @enumb='0']")
    if ($create -ne $null) {
        $code = (@($create.SelectNodes('.//string') | ForEach-Object { $_.InnerText }) -join "`n")
        $m = [regex]::Match($code, '(?m)^[ \t]*image_speed[ \t]*=[ \t]*([0-9]*\.?[0-9]+)[ \t]*;?')
        if ($m.Success) { $objectCreateSpeed[$name] = [double]::Parse($m.Groups[1].Value, $inv) }
    }
}

# --- mirror files ------------------------------------------------------------------------------------
$written = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$copied = 0
function Mirror([string]$from, [string]$assetPath) {
    $to = Join-Path $root $assetPath
    [void]$written.Add([IO.Path]::GetFullPath($to))
    if (Test-Path $to) {
        $a = Get-Item $from; $b = Get-Item $to
        if ($a.Length -eq $b.Length -and (Get-FileHash $from).Hash -eq (Get-FileHash $to).Hash) { return }
    }
    New-Item -ItemType Directory -Force (Split-Path -Parent $to) | Out-Null
    Copy-Item $from $to -Force
    $script:copied++
}
function Join-Asset([string[]]$parts) { return (@($parts | Where-Object { $_ }) -join '/') }

# --- sprites: every *.sprite.gmx on disk ---------------------------------------------------------------
$sprites = @(); $skipped = @()
foreach ($file in Get-ChildItem (Join-Path $Source 'sprites') -Filter '*.sprite.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.sprite\.gmx$', ''
    $inProject = $spriteGroup.ContainsKey($name)
    $group = if ($inProject) { $spriteGroup[$name] } else { $unlisted }
    $s = (Read-Xml $file.FullName).sprite
    $width = [int]$s.width; $height = [int]$s.height
    $frameNodes = @($s.SelectNodes('frames/frame') | Sort-Object { [int]$_.GetAttribute('index') })
    if ($frameNodes.Count -eq 0) { $skipped += [ordered]@{ name = $name; kind = 'sprite'; reason = 'the sprite has no frames' }; continue }
    $problem = $null
    foreach ($f in $frameNodes) {
        $img = Join-Path (Join-Path $Source 'sprites') $f.InnerText
        if (-not (Test-Path $img)) { $problem = "frame image $($f.InnerText) is missing"; break }
        $size = Png-Size $img
        if ($size -eq $null) { $problem = "frame image $($f.InnerText) is not a PNG"; break }
        if ($size[0] -ne $width -or $size[1] -ne $height) { $problem = "frame image $($f.InnerText) is $($size[0])x$($size[1]), not the sprite's ${width}x${height}"; break }
    }
    if ($problem) { $skipped += [ordered]@{ name = $name; kind = 'sprite'; reason = $problem }; continue }

    $folder = Join-Asset @("$artAssets/Sprites", $group, $name)
    $frames = @(); $sourceFrames = @()
    for ($i = 0; $i -lt $frameNodes.Count; $i++) {
        $asset = "$folder/${name}_$i.png"
        Mirror (Join-Path (Join-Path $Source 'sprites') $frameNodes[$i].InnerText) $asset
        $frames += $asset
        $sourceFrames += ('sprites\' + $frameNodes[$i].InnerText)
    }

    # image_speed: the first object showing this sprite whose Create sets one; GameMaker's default 1 otherwise.
    $owners = @($objectSprite.Keys | Where-Object { $objectSprite[$_] -eq $name } | Sort-Object)
    $speedOwners = @($owners | Where-Object { $objectCreateSpeed.ContainsKey($_) })
    $notes = @()
    if (-not $inProject) { $notes += 'on disk but not in Ricochet.project.gmx, so GameMaker never built it into the game; imported so every file the original shipped is in the catalogue' }
    if ($speedOwners.Count -gt 0) {
        $imageSpeed = $objectCreateSpeed[$speedOwners[0]]
        $distinct = @($speedOwners | ForEach-Object { $objectCreateSpeed[$_] } | Sort-Object -Unique)
        if ($distinct.Count -gt 1) { $notes += "objects disagree on image_speed ($(($speedOwners | ForEach-Object { "$_=$($objectCreateSpeed[$_].ToString($inv))" }) -join ', ')); the catalogue uses $($speedOwners[0])'s" }
        $speedSource = ($speedOwners | ForEach-Object { "$_ Create" }) -join ', '
    } else {
        $imageSpeed = 1.0
        $speedSource = 'GameMaker default (no object showing it sets image_speed in Create)'
    }

    $atlas = ''
    if ($width -gt $atlasMaxSide -or $height -gt $atlasMaxSide) {
        $notes += "${width}x$height is larger than $atlasMaxSide px on a side, so it keeps its own texture instead of joining a sprite atlas"
    } else {
        $groupParts = @($group -split '/' | Where-Object { $_ })
        $key = if ($groupParts.Count -eq 0) { 'root' } else { ($groupParts[0] -replace '[^A-Za-z0-9]+', '') }
        $atlas = "$artAssets/Atlases/$atlasPrefix$key.spriteatlasv2"
    }

    $sprites += [ordered]@{
        name = $name; group = $group; inProject = $inProject
        originalPath = "sprites\$name.sprite.gmx"
        originalFrames = $sourceFrames
        width = $width; height = $height
        xorig = [int]$s.xorig; yorig = [int]$s.yorigin
        colkind = [int]$s.colkind; sepmasks = ([int]$s.sepmasks -ne 0)
        bboxLeft = [int]$s.bbox_left; bboxTop = [int]$s.bbox_top; bboxRight = [int]$s.bbox_right; bboxBottom = [int]$s.bbox_bottom
        tileHorizontal = ([int]$s.HTile -ne 0); tileVertical = ([int]$s.VTile -ne 0)
        imageSpeed = $imageSpeed; imageSpeedSource = $speedSource
        objects = $owners
        frames = $frames; atlas = $atlas
        notes = $notes
    }
}
foreach ($n in $spriteGroup.Keys) {
    if (-not (Test-Path (Join-Path $Source "sprites\$n.sprite.gmx"))) {
        $skipped += [ordered]@{ name = $n; kind = 'sprite'; reason = "listed in Ricochet.project.gmx but sprites\$n.sprite.gmx is missing" }
    }
}

# --- backgrounds: every *.background.gmx on disk --------------------------------------------------------
$backgrounds = @()
foreach ($file in Get-ChildItem (Join-Path $Source 'background') -Filter '*.background.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.background\.gmx$', ''
    $inProject = $backgroundGroup.ContainsKey($name)
    $group = if ($inProject) { $backgroundGroup[$name] } else { $unlisted }
    $b = (Read-Xml $file.FullName).background
    $img = Join-Path (Join-Path $Source 'background') $b.data
    $size = if (Test-Path $img) { Png-Size $img } else { $null }
    if ($size -eq $null) { $skipped += [ordered]@{ name = $name; kind = 'background'; reason = "image $($b.data) is missing or not a PNG" }; continue }
    $asset = (Join-Asset @("$artAssets/Backgrounds", $group, "$name.png"))
    Mirror $img $asset
    $notes = @()
    if (-not $inProject) { $notes += 'on disk but not in Ricochet.project.gmx, so GameMaker never built it into the game; imported so every file the original shipped is in the catalogue' }
    $backgrounds += [ordered]@{
        name = $name; group = $group; inProject = $inProject
        originalPath = "background\$name.background.gmx"
        originalImage = ('background\' + $b.data)
        width = $size[0]; height = $size[1]
        tileHorizontal = ([int]$b.HTile -ne 0); tileVertical = ([int]$b.VTile -ne 0)
        tileset = ([int]$b.istileset -ne 0); tileWidth = [int]$b.tilewidth; tileHeight = [int]$b.tileheight
        texture = $asset
        notes = $notes
    }
}
foreach ($n in $backgroundGroup.Keys) {
    if (-not (Test-Path (Join-Path $Source "background\$n.background.gmx"))) {
        $skipped += [ordered]@{ name = $n; kind = 'background'; reason = "listed in Ricochet.project.gmx but background\$n.background.gmx is missing" }
    }
}

# --- delete what the original no longer has ----------------------------------------------------------
$removed = 0
foreach ($sub in 'Sprites', 'Backgrounds') {
    $dir = Join-Path $artDir $sub
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in Get-ChildItem $dir -Recurse -File -Filter '*.png') {
        if (-not $written.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
    foreach ($d in @(Get-ChildItem $dir -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) {
        if (@(Get-ChildItem $d.FullName -Force | Where-Object { $_.Extension -ne '.meta' }).Count -eq 0) {
            Remove-Item $d.FullName -Recurse -Force
            if (Test-Path "$($d.FullName).meta") { Remove-Item "$($d.FullName).meta" -Force }
        }
    }
}

# --- the manifest ------------------------------------------------------------------------------------
. (Join-Path $PSScriptRoot 'gml-json.ps1')
$manifest = [ordered]@{
    source = 'gmlRicochet 09-05-13_2.20am (GameMaker 8.1 / Studio project, "Beta 18"), read by Tools/import-gml-art.ps1; paths below are relative to it'
    pixelsPerUnit = $pixelsPerUnit
    roomSpeed = $roomSpeed
    atlasMaxSide = $atlasMaxSide
    sprites = $sprites
    backgrounds = $backgrounds
    skipped = $skipped
}
New-Item -ItemType Directory -Force $artDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

$frameCount = ($sprites | ForEach-Object { $_.frames.Count } | Measure-Object -Sum).Sum
$listedSprites = @($sprites | Where-Object { $_.inProject }).Count
$listedBackgrounds = @($backgrounds | Where-Object { $_.inProject }).Count
Write-Host "[Ricochet] GML art: $($sprites.Count) sprites ($listedSprites in the project, $frameCount frames), $($backgrounds.Count) backgrounds ($listedBackgrounds in the project), $($skipped.Count) skipped; $copied files copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($s in $skipped) { Write-Host "  skipped $($s.kind) $($s.name): $($s.reason)" }

if (-not $NoUnity) {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'unity.ps1') -Method Ricochet.Tooling.GmlArtBuilder.BuildAll -Log art
    exit $LASTEXITCODE
}
