$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$file = $payload.tool_input.file_path

if (-not $file -or $file -notmatch '\.cs$') { exit 0 }

$root = $env:CLAUDE_PROJECT_DIR
$relative = [IO.Path]::GetRelativePath($root, $file)

Set-Location $root
dotnet format TwitchAPI.slnx --include $relative --verbosity quiet | Out-Null
exit 0