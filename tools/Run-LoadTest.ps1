param([string]$K6Path)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $K6Path) {
    $local = Get-ChildItem (Join-Path $root '.tools/k6') -Recurse -Filter k6.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($local) { $K6Path = $local.FullName } else { $K6Path = (Get-Command k6 -ErrorAction Stop).Source }
}
$resultsRoot = [IO.Path]::GetFullPath((Join-Path $root 'TestResults'))
$runDirectory = Join-Path $resultsRoot ("load-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $runDirectory | Out-Null
$sessionPath = Join-Path $runDirectory 'session.json'
$databasePath = Join-Path $runDirectory 'load.db'
$hostProject = Join-Path $root 'GSB_project/tests/GSB_project.PerformanceHost/GSB_project.PerformanceHost.csproj'
$hostDll = Join-Path $root 'GSB_project/tests/GSB_project.PerformanceHost/bin/Release/net10.0/GSB_project.PerformanceHost.dll'
$variables = @('BASE_URL','EVENT_ID','API_TOKEN','Logging__LogLevel__Default','ASPNETCORE_ENVIRONMENT')
$saved = @{}
foreach ($name in $variables) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
$process = $null
$session = $null
try {
    & dotnet build $hostProject --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Performance host build failed.' }
    $env:Logging__LogLevel__Default = 'Warning'
    $env:ASPNETCORE_ENVIRONMENT = 'Performance'
    $arguments = @(('"' + $hostDll + '"'), ('"--LoadTest:SessionPath=' + $sessionPath + '"'), ('"--LoadTest:DatabasePath=' + $databasePath + '"'))
    $process = Start-Process dotnet -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runDirectory 'host.stdout.log') -RedirectStandardError (Join-Path $runDirectory 'host.stderr.log')
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while (-not (Test-Path -LiteralPath $sessionPath)) {
        if ($process.HasExited) { throw ('Host exited. ' + (Get-Content (Join-Path $runDirectory 'host.stderr.log') -Raw)) }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Host startup timed out.' }
        Start-Sleep -Milliseconds 200
    }
    $session = Get-Content $sessionPath -Raw | ConvertFrom-Json
    $uri = [Uri]$session.baseUrl
    if (-not $uri.IsLoopback) { throw 'Load runner only permits localhost.' }
    $env:BASE_URL = $session.baseUrl
    $env:EVENT_ID = [string]$session.eventId
    $env:API_TOKEN = $session.token
    & $K6Path run --summary-export (Join-Path $runDirectory 'k6-summary.json') (Join-Path $root 'GSB_project/tests/performance/events.k6.js')
    if ($LASTEXITCODE -ne 0) { throw 'k6 thresholds or execution failed.' }
    Write-Output "Load test passed. Results: $runDirectory"
}
finally {
    if ($process -and -not $process.HasExited) {
        if ($session) {
            try { Invoke-RestMethod -Method Post -Uri ($session.baseUrl + '/local-test/shutdown') -Headers @{ Authorization = ('Bearer ' + $session.token) } -TimeoutSec 5 | Out-Null } catch { }
        }
        if (-not $process.WaitForExit(5000)) { Stop-Process -Id $process.Id -Force }
        $process.WaitForExit()
    }
    # Exact generated file paths, verified inside this run's TestResults directory.
    foreach ($file in @($sessionPath, $databasePath, ($databasePath + '-wal'), ($databasePath + '-shm'))) {
        $resolved = [IO.Path]::GetFullPath($file)
        if (-not $resolved.StartsWith($resultsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup path outside TestResults.' }
        if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Force }
    }
    foreach ($name in $variables) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
