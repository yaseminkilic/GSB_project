param([string]$Version = '2.4.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid version.' }
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root '.tools/k6'
New-Item -ItemType Directory -Force $directory | Out-Null
$name = "k6-v$Version-windows-amd64.zip"
$base = "https://github.com/grafana/k6/releases/download/v$Version"
$archive = Join-Path $directory $name
$checksums = Join-Path $directory 'checksums.txt'
Invoke-WebRequest "$base/$name" -OutFile $archive
Invoke-WebRequest "$base/k6-v$Version-checksums.txt" -OutFile $checksums
$line = Get-Content $checksums | Where-Object { $_ -match ([regex]::Escape($name) + '$') }
if (@($line).Count -ne 1) { throw 'Checksum entry missing or ambiguous.' }
$expected = ($line.Trim() -split '\s+')[0]
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $expected) { throw 'SHA256 mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $directory -Force
$binary = Get-ChildItem $directory -Recurse -Filter k6.exe | Select-Object -First 1
if (-not $binary) { throw 'k6.exe not found.' }
& $binary.FullName version
if ($LASTEXITCODE -ne 0) { throw 'k6 failed to start.' }
