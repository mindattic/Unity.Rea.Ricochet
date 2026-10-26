# Shared by the tools/import-gml-*.ps1 scripts: writes PowerShell values (ordered dictionaries, arrays, strings,
# numbers, booleans) as stable, indented JSON, so rerunning an import on the same source writes the same bytes.
$inv = [Globalization.CultureInfo]::InvariantCulture
function To-Json($value, [string]$indent) {
    $inner = $indent + '  '
    if ($value -eq $null) { return 'null' }
    if ($value -is [bool]) { return $(if ($value) { 'true' } else { 'false' }) }
    if ($value -is [int] -or $value -is [long]) { return $value.ToString($inv) }
    if ($value -is [double] -or $value -is [single]) { return ([double]$value).ToString('R', $inv) }
    if ($value -is [string]) {
        $sb = New-Object Text.StringBuilder
        foreach ($c in $value.ToCharArray()) {
            switch ($c) {
                '"' { [void]$sb.Append('\"') } '\' { [void]$sb.Append('\\') }
                "`n" { [void]$sb.Append('\n') } "`r" { [void]$sb.Append('\r') } "`t" { [void]$sb.Append('\t') }
                default { if ([int]$c -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', [int]$c) } else { [void]$sb.Append($c) } }
            }
        }
        return '"' + $sb.ToString() + '"'
    }
    if ($value -is [Collections.IDictionary]) {
        if ($value.Count -eq 0) { return '{}' }
        $items = foreach ($k in $value.Keys) { $inner + '"' + $k + '": ' + (To-Json $value[$k] $inner) }
        return "{`n" + ($items -join ",`n") + "`n$indent}"
    }
    if ($value -is [Collections.IEnumerable]) {
        $list = @($value)
        if ($list.Count -eq 0) { return '[]' }
        if (-not ($list | Where-Object { $_ -is [Collections.IDictionary] })) {
            return '[' + ((@($list | ForEach-Object { To-Json $_ $inner })) -join ', ') + ']'
        }
        $items = foreach ($v in $list) { $inner + (To-Json $v $inner) }
        return "[`n" + ($items -join ",`n") + "`n$indent]"
    }
    throw "Cannot write $($value.GetType()) as JSON"
}
