<#
.SYNOPSIS
  Imports the original's nine fonts and its sprite font (gmlRicochet's font resources and spr_spritefont) into
  Assets/Rea.Ricochet/Fonts (U.RICO-T44).
.DESCRIPTION
  GameMaker never ships a font file: at build time it rasterises each font resource once, at its size and style,
  into a glyph atlas (fonts/<name>.png, white glyphs on transparency) and a glyph table (fonts/<name>.font.gmx: each
  character's cell in the atlas, its advance "shift" and left bearing "offset"), and the game draws text from those.
  This script imports exactly that, never the typeface itself (Impact, Verdana, Monotype Corsiva and Arial are not
  ours to redistribute; see the licences section of the manifest and docs/BIBLE.md).

  It reads Ricochet.project.gmx (which fonts are in the resource tree), every fonts/*.font.gmx (face name, size, bold,
  italic, anti-aliasing, glyphs, atlas image) and every object and script for its draw_set_font calls (outside
  comments), mirrors each atlas to Assets/Rea.Ricochet/Fonts/Atlases/<name>.png (copying only changed bytes, deleting
  atlases the original no longer has), and writes Assets/Rea.Ricochet/Fonts/gml-fonts.json: one entry per font with its
  fields, glyphs, atlas, original path, the objects that draw with it and any notes, the sprite font's grid, the
  outline draw_text_outline draws, the licence of each typeface, and a skipped list.

  The sprite font is spr_spritefont, a 440x596 sheet of coloured glyphs (ASCII 33 to 126 in a 10-column grid of
  40x51 px cells, ruled with 1 px lines, starting at 20,20). No code in the original calls font_add_sprite on it and
  the project file does not list it, but it is the one font the original's files carry as a sprite, so it is imported
  as a font as well.

  Unity then does the rest from that file: the editor's GmlFontBuilder turns each atlas into a signed distance field
  (so TextMeshPro can draw draw_text_outline's outline) and builds one TMP_FontAsset per font, with a plain and an
  outlined material, under Resources/Ricochet/Fonts, and records them in the ArtCatalogue. Unless -NoUnity is given
  this script runs GmlFontBuilder headless through Tools/unity.ps1 once the files are in place.

  Running it twice with the same source changes nothing.
.EXAMPLE
  powershell -File Tools/import-gml-fonts.ps1
#>
param(
    [string]$Source = 'R:\Projects\_PRE_2022\_PRE_2021\_PRE_2020\_PRE_2017\gmlRicochet\09-05-13_2.20am',
    [switch]$NoUnity
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2
. (Join-Path $PSScriptRoot 'gml-json.ps1')

$root = Split-Path -Parent $PSScriptRoot
$fontAssets = 'Assets/Rea.Ricochet/Fonts'
$fontDir = Join-Path $root $fontAssets
$atlasDir = Join-Path $fontDir 'Atlases'
$manifestPath = Join-Path $fontDir 'gml-fonts.json'
$projectFile = Join-Path $Source 'Ricochet.project.gmx'

if (-not (Test-Path $projectFile)) { throw "No Ricochet.project.gmx under $Source" }

function Read-Xml([string]$path) {
    $doc = New-Object Xml.XmlDocument
    $doc.Load($path)
    return $doc
}

# A PNG's width and height from its IHDR chunk.
function Png-Size([string]$path) {
    $b = [IO.File]::ReadAllBytes($path)
    if ($b.Length -lt 24 -or $b[1] -ne 0x50 -or $b[2] -ne 0x4E -or $b[3] -ne 0x47) { return $null }
    $w = ([int]$b[16] -shl 24) -bor ([int]$b[17] -shl 16) -bor ([int]$b[18] -shl 8) -bor [int]$b[19]
    $h = ([int]$b[20] -shl 24) -bor ([int]$b[21] -shl 16) -bor ([int]$b[22] -shl 8) -bor [int]$b[23]
    return @($w, $h)
}

# The typefaces the font resources name, and what their licences allow.
$licences = @(
    [ordered]@{
        family = 'Impact'
        owner = 'Monotype (designed by Geoffrey Lee, 1965, for Stephenson Blake); licensed to Microsoft, which ships it with Windows'
        licence = 'Proprietary. The Windows licence lets the font be used on that machine, not redistributed as a font file.'
        rebuild = 'Not shipped. Only GameMaker''s rasterised atlases of it (fnt_impact, fnt_score, fnt_score_shadow, fnt_count, fnt_small) are imported, which is what the original game itself shipped.'
    },
    [ordered]@{
        family = 'Verdana'
        owner = 'Microsoft (designed by Matthew Carter, 1996)'
        licence = 'Proprietary. Shipped with Windows for use on that machine; the font file may not be redistributed.'
        rebuild = 'Not shipped. Only GameMaker''s rasterised atlas of it (fnt_credits) is imported.'
    },
    [ordered]@{
        family = 'Monotype Corsiva'
        owner = 'Monotype (designed by Patricia Saunders, 1995); licensed to Microsoft, which ships it with Office'
        licence = 'Proprietary. Installed with Office for use on that machine; the font file may not be redistributed.'
        rebuild = 'Not shipped. Only GameMaker''s rasterised atlas of it (fnt_mono) is imported.'
    },
    [ordered]@{
        family = 'Arial'
        owner = 'Monotype (designed by Robin Nicholas and Patricia Saunders, 1982); licensed to Microsoft, which ships it with Windows'
        licence = 'Proprietary. The Windows licence lets the font be used on that machine, not redistributed as a font file.'
        rebuild = 'Not shipped. Only GameMaker''s rasterised atlases of it (font4, font5) are imported.'
    },
    [ordered]@{
        family = 'spr_spritefont'
        owner = 'A sprite sheet in the original project (its caption: "a typical sprite font")'
        licence = 'Part of the original project''s own files.'
        rebuild = 'Imported as the sheet itself.'
    }
)

# --- the resource tree -----------------------------------------------------------------------------
$project = Read-Xml $projectFile
$inTree = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($node in $project.SelectNodes('//fonts//font')) { [void]$inTree.Add(($node.InnerText -replace '^fonts\\', '')) }
$spritesInTree = New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
foreach ($node in $project.SelectNodes('//sprites//sprite')) { [void]$spritesInTree.Add(($node.InnerText -replace '^sprites\\', '')) }

# --- who draws with which font: every draw_set_font(<font>) outside a comment -----------------------
function Strip-Comments([string]$code) {
    $code = [regex]::Replace($code, '/\*.*?\*/', '', 'Singleline')
    return [regex]::Replace($code, '//[^\r\n]*', '')
}
$usedBy = @{}
$outlineSizes = @{}
function Note-Uses([string]$owner, [string]$code) {
    $code = Strip-Comments $code
    foreach ($m in [regex]::Matches($code, 'draw_set_font\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*\)')) {
        $font = $m.Groups[1].Value
        if (-not $usedBy.ContainsKey($font)) { $usedBy[$font] = New-Object 'Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal) }
        [void]$usedBy[$font].Add($owner)
    }
    # draw_text_outline(x, y, size, text): count the outline sizes it is called with.
    foreach ($m in [regex]::Matches($code, 'draw_text_outline\s*\(\s*[^,]+,\s*[^,]+,\s*([^,]+),')) {
        $size = $m.Groups[1].Value.Trim()
        if (-not $outlineSizes.ContainsKey($size)) { $outlineSizes[$size] = 0 }
        $outlineSizes[$size]++
    }
}
foreach ($file in Get-ChildItem (Join-Path $Source 'objects') -Filter '*.object.gmx') {
    $owner = $file.Name -replace '\.object\.gmx$', ''
    foreach ($s in (Read-Xml $file.FullName).SelectNodes('//argument/string')) { Note-Uses $owner $s.InnerText }
}
foreach ($file in Get-ChildItem (Join-Path $Source 'scripts') -Filter '*.gml') {
    if ($file.BaseName -eq 'draw_text_outline') { continue }
    Note-Uses ($file.Name -replace '\.gml$', '') ([IO.File]::ReadAllText($file.FullName))
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

# --- fonts -------------------------------------------------------------------------------------------
$fonts = @(); $skipped = @()
foreach ($file in Get-ChildItem (Join-Path $Source 'fonts') -Filter '*.font.gmx' | Sort-Object Name) {
    $name = $file.Name -replace '\.font\.gmx$', ''
    $f = (Read-Xml $file.FullName).font
    $image = [string]$f.image
    $png = Join-Path (Join-Path $Source 'fonts') $image
    if (-not $image -or -not (Test-Path $png)) { $skipped += [ordered]@{ name = $name; kind = 'font'; reason = "atlas fonts\$image is missing" }; continue }
    $size = Png-Size $png
    if ($size -eq $null) { $skipped += [ordered]@{ name = $name; kind = 'font'; reason = "fonts\$image is not a PNG" }; continue }

    $glyphs = @($f.glyphs.glyph | Sort-Object { [int]$_.character } | ForEach-Object {
        [ordered]@{
            character = [int]$_.character
            x = [int]$_.x; y = [int]$_.y; w = [int]$_.w; h = [int]$_.h
            shift = [int]$_.shift; offset = [int]$_.offset
        }
    })
    $bad = @($glyphs | Where-Object { $_.x -lt 0 -or $_.y -lt 0 -or $_.x + $_.w -gt $size[0] -or $_.y + $_.h -gt $size[1] })
    if ($bad.Count -gt 0) { $skipped += [ordered]@{ name = $name; kind = 'font'; reason = "$($bad.Count) glyph cells fall outside the $($size[0])x$($size[1]) atlas" }; continue }

    $notes = @()
    $inResourceTree = $inTree.Contains($name)
    if (-not $inResourceTree) { $notes += 'on disk under fonts\ but not in Ricochet.project.gmx, so GameMaker never built it into the game; imported anyway so all nine fonts the original carries are in the catalogue' }
    $users = @(if ($usedBy.ContainsKey($name)) { $usedBy[$name] })
    if ($users.Count -eq 0) { $notes += 'no object or script calls draw_set_font with it' }

    $asset = "$fontAssets/Atlases/$name.png"
    Mirror $png $asset

    $fonts += [ordered]@{
        name = $name
        inResourceTree = $inResourceTree
        originalPath = "fonts\$name.font.gmx"
        originalImage = "fonts\$image"
        family = [string]$f.name
        size = [int]$f.size
        bold = ([int]$f.bold -ne 0)
        italic = ([int]$f.italic -ne 0)
        antialias = [int]$f.aa
        atlas = $asset
        atlasWidth = $size[0]
        atlasHeight = $size[1]
        usedBy = $users
        notes = $notes
        glyphs = $glyphs
    }
}
foreach ($n in $inTree) {
    if (-not ($fonts | Where-Object { $_.name -eq $n }) -and -not ($skipped | Where-Object { $_.name -eq $n })) {
        $skipped += [ordered]@{ name = $n; kind = 'font'; reason = "listed in Ricochet.project.gmx but fonts\$n.font.gmx is missing" }
    }
}
foreach ($k in $usedBy.Keys) {
    if (-not ($fonts | Where-Object { $_.name -eq $k })) {
        $skipped += [ordered]@{ name = $k; kind = 'draw_set_font'; reason = "drawn with by $(@($usedBy[$k]) -join ', ') but no such font resource" }
    }
}

# --- the sprite font ---------------------------------------------------------------------------------
$spriteFonts = @()
$sheetName = 'spr_spritefont'
$sheetXml = Join-Path $Source "sprites\$sheetName.sprite.gmx"
if (Test-Path $sheetXml) {
    $frame = (Read-Xml $sheetXml).sprite.frames.frame
    $sheet = Join-Path (Join-Path $Source 'sprites') ([string]$frame.InnerText)
    $sheetSize = Png-Size $sheet
    $asset = "$fontAssets/Atlases/$sheetName.png"
    Mirror $sheet $asset
    $spriteFonts += [ordered]@{
        name = $sheetName
        inResourceTree = $spritesInTree.Contains($sheetName)
        originalPath = "sprites\$sheetName.sprite.gmx"
        originalImage = ('sprites\' + [string]$frame.InnerText)
        atlas = $asset
        atlasWidth = $sheetSize[0]
        atlasHeight = $sheetSize[1]
        firstCharacter = 33
        characterCount = 94
        columns = 10
        originX = 20
        originY = 20
        cellWidth = 40
        cellHeight = 51
        rule = 1
        notes = @('the sheet''s own caption: "a typical sprite font, where each sub image is one letter 40px wide by 51px tall; it covers the ASCII range of 33 - 128"; the grid holds 94 characters (33 to 126)',
                  'no code calls font_add_sprite with it and Ricochet.project.gmx does not list it')
    }
} else {
    $skipped += [ordered]@{ name = $sheetName; kind = 'sprite font'; reason = "sprites\$sheetName.sprite.gmx is missing" }
}

# --- delete what the original no longer has ----------------------------------------------------------
$removed = 0
if (Test-Path $atlasDir) {
    foreach ($f in Get-ChildItem $atlasDir -File | Where-Object { $_.Extension -ne '.meta' }) {
        if (-not $written.Contains($f.FullName)) {
            Remove-Item $f.FullName -Force
            if (Test-Path "$($f.FullName).meta") { Remove-Item "$($f.FullName).meta" -Force }
            $removed++
        }
    }
}

# --- the manifest ------------------------------------------------------------------------------------
$sizes = @(foreach ($k in ($outlineSizes.Keys | Sort-Object)) { [ordered]@{ size = $k; calls = $outlineSizes[$k] } })
$manifest = [ordered]@{
    source = 'gmlRicochet 09-05-13_2.20am (GameMaker 8.1 / Studio project, "Beta 18"), read by Tools/import-gml-fonts.ps1; paths below are relative to it'
    outline = [ordered]@{
        script = 'scripts\draw_text_outline.gml'
        sizes = @(1, 2)
        what = 'draw_text_outline(x, y, size, text) draws the text in black at every whole-pixel offset up to size in x and y, then in white on top, and restores the colour; the rebuild draws it as a black SDF outline size px wide around white text, one outlined material per size the original calls it with'
        calls = $sizes
    }
    licences = $licences
    fonts = $fonts
    spriteFonts = $spriteFonts
    skipped = $skipped
}
New-Item -ItemType Directory -Force $fontDir | Out-Null
$json = (To-Json $manifest '') + "`n"
$old = if (Test-Path $manifestPath) { [IO.File]::ReadAllText($manifestPath) } else { '' }
if ($old -ne $json) { [IO.File]::WriteAllText($manifestPath, $json, (New-Object Text.UTF8Encoding $false)) }

Write-Host "[Ricochet] GML fonts: $($fonts.Count) fonts and $($spriteFonts.Count) sprite font, $($skipped.Count) skipped; $copied atlases copied, $removed removed; manifest $(if ($old -ne $json) { 'updated' } else { 'unchanged' })"
foreach ($f in $fonts) { Write-Host ("  {0,-16} {1} {2}, {3} glyphs, {4}x{5}; used by {6}" -f $f.name, $f.family, $f.size, $f.glyphs.Count, $f.atlasWidth, $f.atlasHeight, $(if ($f.usedBy.Count) { $f.usedBy.Count.ToString() + ' objects' } else { 'nothing' })) }
Write-Host "  draw_text_outline sizes: $(($sizes | ForEach-Object { "$($_.size) x$($_.calls)" }) -join ', ')"
foreach ($s in $skipped) { Write-Host "  skipped $($s.kind) $($s.name): $($s.reason)" }

if (-not $NoUnity) {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot 'unity.ps1') -Method Ricochet.Tooling.GmlFontBuilder.BuildAll -Log fonts
    exit $LASTEXITCODE
}
