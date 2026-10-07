param(
  [string]$ExePath = "src/BATIR/bin/Any CPU/Release/net48/BATIR.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ExePath)) {
  throw "BATIR.exe was not produced: $ExePath"
}

$info = Get-Item $ExePath
if ($info.Length -lt 50000) {
  throw "BATIR.exe is unexpectedly small: $($info.Length) bytes"
}

& $ExePath --self-test
if ($LASTEXITCODE -ne 0) {
  throw "BATIR database self-test failed with exit code $LASTEXITCODE"
}

Write-Host "Stage 6 smoke test passed."
Write-Host "Executable: $($info.FullName)"
Write-Host "Size: $($info.Length) bytes"
