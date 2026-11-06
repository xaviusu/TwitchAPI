$ErrorActionPreference = 'Stop'

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$file = $payload.tool_input.file_path

if (-not $file -or $file -notmatch '\.cs$') { exit 0 }

$projectDir = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
$root = (Resolve-Path $projectDir).Path.TrimEnd('\', '/')
$full = (Resolve-Path $file).Path

if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { exit 0 }

$relative = $full.Substring($root.Length).TrimStart('\', '/')

Set-Location $root
dotnet format TwitchAPI.slnx --include $relative --verbosity quiet | Out-Null
if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine("dotnet format failed for $relative"); exit 1 }
exit 0