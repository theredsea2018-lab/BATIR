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

$selfTestOutput = & $ExePath --self-test 2>&1 | Out-String
$selfTestExitCode = $LASTEXITCODE
Write-Host "Stage 6 self-test output:"
Write-Host $selfTestOutput
Write-Host "Stage 6 self-test exit code: $selfTestExitCode"
if ($selfTestExitCode -ne 0) {
  throw "BATIR database self-test failed with exit code $selfTestExitCode"
}

Write-Host "Stage 6 smoke test passed."
Write-Host "Executable: $($info.FullName)"
Write-Host "Size: $($info.Length) bytes"
