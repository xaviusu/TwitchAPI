$ErrorActionPreference = 'Stop'

$root = (git rev-parse --show-toplevel).Trim()
Set-Location $root

$missing = @()
foreach ($line in git ls-files --eol --cached --others --exclude-standard) {
    $info, $path = $line -split "`t", 2
    if ($info -match 'w/(-text|none)') { continue }
    $full = Join-Path $root $path
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
    $bytes = [IO.File]::ReadAllBytes($full)
    if ($bytes.Length -gt 0 -and $bytes[$bytes.Length - 1] -ne 10) { $missing += $path }
}

if ($missing.Count -gt 0) {
    [Console]::Error.WriteLine("Missing final newline (.editorconfig insert_final_newline):`n" + ($missing -join "`n"))
    exit 1
}

exit 0
