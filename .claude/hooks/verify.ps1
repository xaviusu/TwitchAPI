$null = [Console]::In.ReadToEnd()
Set-Location $env:CLAUDE_PROJECT_DIR

$changed = git status --porcelain -- '*.cs' '*.csproj' '*.props'
if (-not $changed) { exit 0 }

function Fail($step, $output) {
    $tail = ($output | Select-Object -Last 40) -join "`n"
    [Console]::Error.WriteLine("Definition of done not met: $step failed.`n$tail")
    exit 2
}

$out = dotnet build --nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) { Fail 'dotnet build' $out }

$out = dotnet test --nologo --no-build -v q 2>&1
if ($LASTEXITCODE -ne 0) { Fail 'dotnet test' $out }

$out = dotnet format --verify-no-changes -v q 2>&1
if ($LASTEXITCODE -ne 0) { Fail 'dotnet format --verify-no-changes' $out }

exit 0