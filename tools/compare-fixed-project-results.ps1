$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')))
$python='C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$homeDir='artifacts/fixed-project-check'
$forward=Get-Content "$homeDir/forward-result.json" -Raw|ConvertFrom-Json
& $python tools/compare-project-packages.py (Join-Path $forward.RunDirectory 'Transfer') "$homeDir/Reexport" "$homeDir/resource-comparison.json"
if($LASTEXITCODE -ne 0){throw 'Saved project resource comparison failed'}
$baseline='artifacts/runtime-ui-api-tests/extended-original-audited-v3/unity-execution.json'
$package=Join-Path $forward.RunDirectory 'Transfer/units/s_99c9720ab356a0642a771bea13969a05/package.json'
& ./tools/compare-runtime-results.ps1 -ExpectedFile $baseline -NativeFile "$homeDir/Legacy/scene-execution.json" -PackageFile $package -Output "$homeDir/Legacy"
& $python tools/verify-fixed-project.py
if($LASTEXITCODE -ne 0){throw 'Actual 55-script project gate failed'}
