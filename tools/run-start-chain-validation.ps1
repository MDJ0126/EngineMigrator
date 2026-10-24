param([switch]$RegressionsOnly,[switch]$WorldOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $root
$python='C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
$runner='artifacts/start-chain-check/runner/bin/Release/net9.0/EngineMigrator.Checks.dll'
dotnet $runner verify-source;if($LASTEXITCODE -ne 0){throw 'Source verification failed'}
& $python tools/verify-start-chain-probe.py;if($LASTEXITCODE -ne 0){throw 'Startup roundtrip failed'}
$cases=@('single-100-20','single-50-20','single-30-20','single-30-10')
foreach($mode in @('rate','scaled','late','prefab','mutation','scenelate','chain')){foreach($fps in @(100,30)){$cases+="$mode-$fps-20"}}
if(-not $WorldOnly){foreach($case in $cases){dotnet $runner $case;if($LASTEXITCODE -ne 0){throw "Native regression failed: $case"};Write-Output "CAPTURED $case"}}
& $python tools/verify-start-chain-regressions.py;if($LASTEXITCODE -ne 0){throw 'Native regression comparison failed'}
if($RegressionsOnly){return}
$worldRunner='artifacts/start-chain-check/world49-runner/bin/Release/net9.0/EngineMigrator.Checks.dll'
dotnet $worldRunner verify-executed-source;if($LASTEXITCODE -ne 0){throw 'Actual Unity source identity failed'}
dotnet $worldRunner audit;if($LASTEXITCODE -ne 0){throw 'Read-only safety audit failed'}
dotnet $worldRunner build;if($LASTEXITCODE -ne 0){throw '49-script native build failed'}
$project=Join-Path $root 'artifacts/start-chain-check/world49/Project'
$inputs=@(Get-ChildItem (Join-Path $project 'Source') -Recurse -File)+@(Get-ChildItem (Join-Path $project 'Plugins') -Recurse -File|Where-Object {$_.FullName -like '*\Source\*' -or $_.Extension -eq '.dll'})+@(Get-ChildItem (Join-Path $project 'Binaries') -Recurse -File -Filter '*.dll')
$hashes=[ordered]@{};foreach($f in $inputs){$hashes[$f.FullName]=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash}
$hashes|ConvertTo-Json -Depth 3|Set-Content artifacts/start-chain-check/world49/build-hashes.json -Encoding utf8
foreach($mode in @('world-original','world-native','world-restored','legacy-regression')){dotnet $worldRunner $mode;if($LASTEXITCODE -ne 0){throw "49-script execution failed: $mode"};Write-Output "CAPTURED $mode"}
& $python tools/verify-start-chain-world49.py;if($LASTEXITCODE -ne 0){throw '49-script project comparison failed'}
$baseline='artifacts/runtime-ui-api-tests/extended-original-audited-v3/unity-execution.json'
$legacy='artifacts/start-chain-check/world49/legacy-regression'
$manifest=Get-Content (Join-Path $project '.EngineMigratorProject.json') -Raw|ConvertFrom-Json
$unit=$manifest.units|Where-Object id -eq 's_99c9720ab356a0642a771bea13969a05'
if(-not $unit){throw 'Legacy scene manifest entry missing'}
# Compare against the validated original transfer for this same scene identity.
$package='artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Transfer/units/s_99c9720ab356a0642a771bea13969a05/package.json'
if(-not(Test-Path $package)){throw 'Legacy reference package missing'}
& ./tools/compare-runtime-results.ps1 -ExpectedFile $baseline -NativeFile "$legacy/scene-execution.json" -PackageFile $package -Output $legacy
