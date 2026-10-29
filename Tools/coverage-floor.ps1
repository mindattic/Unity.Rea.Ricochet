# Fails when line coverage of Rea.Ricochet.Core (a Cobertura report from `dotnet test --collect:"XPlat Code Coverage"`)
# is under the floor, and lists the least-covered types so the next tests have a target (U.RICO-T40).
#   dotnet test Core.slnx --collect:"XPlat Code Coverage" --results-directory TestResults
#   pwsh Tools/coverage-floor.ps1 -Report TestResults -Floor 92
# Raise the floor as coverage grows; it must never be lowered to make a build pass.
param(
    [Parameter(Mandatory)] [string] $Report,   # a coverage.cobertura.xml, or a folder holding one (searched recursively)
    [double] $Floor = 92.0                      # minimum line coverage, percent
)
$ErrorActionPreference = 'Stop'
$file = if (Test-Path $Report -PathType Leaf) { Get-Item $Report }
        else { Get-ChildItem $Report -Recurse -Filter coverage.cobertura.xml | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
if (-not $file) { throw "No coverage.cobertura.xml under '$Report'." }
[xml]$xml = Get-Content $file.FullName
$rows = foreach ($class in $xml.SelectNodes('//class')) {
    $lines = @($class.SelectNodes('lines/line'))
    if ($lines.Count -eq 0) { continue }
    $hit = @($lines | Where-Object { [int]$_.hits -gt 0 }).Count
    [pscustomobject]@{ Type = $class.name; Missed = $lines.Count - $hit; Percent = [math]::Round(100.0 * $hit / $lines.Count, 1) }
}
$valid = [int]$xml.coverage.'lines-valid'
$covered = [int]$xml.coverage.'lines-covered'
$total = [math]::Round(100.0 * $covered / $valid, 2)
Write-Host "Least covered types:"
$rows | Sort-Object Percent, @{ Expression = 'Missed'; Descending = $true } | Select-Object -First 10 | Format-Table -AutoSize | Out-String | Write-Host
Write-Host "Rea.Ricochet.Core line coverage: $total% ($covered of $valid lines), floor $Floor%"
if ($total -lt $Floor) { Write-Error "Coverage $total% is under the $Floor% floor."; exit 1 }
