$ErrorActionPreference='Stop'
Set-Location -LiteralPath ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..')))
$runner='artifacts/fixed-spawn-check/runner/bin/Release/net9.0/EngineMigrator.Checks.dll'
dotnet $runner verify-source;if($LASTEXITCODE -ne 0){throw 'Current fixture source verification failed'}
$cases=@('single-100-20','single-50-20','single-30-20','single-30-10')
foreach($mode in @('rate','scaled','late','prefab','mutation','scenelate','chain','boot')){foreach($fps in @(100,30)){$cases+="$mode-$fps-20"}}
foreach($case in $cases){dotnet $runner $case;if($LASTEXITCODE -ne 0){throw "Regression execution failed $case"};Write-Output "CAPTURED $case"}
& 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/verify-fixed-spawn-regressions.py
if($LASTEXITCODE -ne 0){throw 'Regression comparison failed'}
