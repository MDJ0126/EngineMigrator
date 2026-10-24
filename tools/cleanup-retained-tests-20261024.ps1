$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$art=Join-Path $root 'artifacts'
$lease=[IO.File]::Open((Join-Path $art 'offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
 $latest=Join-Path $art 'EngineMigrator-fixed-spawn-preview-20261024'
 $proof=Get-Content (Join-Path $art 'fixed-spawn-archive-verification.json') -Raw|ConvertFrom-Json
 if($proof.status -ne 'PASS' -or -not(Test-Path (Join-Path $latest 'EngineMigrator.App.exe'))){throw 'Verified release required'}
 $before=[long]((Get-ChildItem $root -Recurse -File -Force|Measure-Object Length -Sum).Sum)
 $protectedFiles=@(Get-ChildItem $latest -Recurse -File)+@(Get-Item ($latest+'.zip'))
 $hashes=@{};foreach($f in $protectedFiles){$hashes[$f.FullName]=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash}
 $targets=@()
 # Keep current common-engine build cache. Other Intermediate directories are reproducible.
 foreach($d in Get-ChildItem $art -Recurse -Directory -Filter Intermediate){if($d.FullName -notlike '*\core-engine-launch-tests\*'){$targets+=$d.FullName}}
 # Keep offline package caches and all source assets, scripts, captures and engine receipts.
 foreach($d in Get-ChildItem $art -Recurse -Directory -Filter Library){
  if($d.FullName -eq (Join-Path $art 'runtime-ui-api-tests\UnitySource\Library')){continue}
  if(Test-Path (Join-Path $d.Parent.FullName 'Assets')){foreach($child in Get-ChildItem $d.FullName -Directory -Force){if($child.Name -notin @('PackageCache','PackageManager')){$targets+=$child.FullName}}}
 }
 $keep=@('fixed-spawn-check','fixed-project-check','start-chain-check','integrated-project-check','core-engine-launch-tests','ui-equality-check','scaled-prefab-check')
 foreach($d in Get-ChildItem $art -Directory){if($d.Name -like '*-check' -and $d.Name -notin $keep){foreach($runner in Get-ChildItem $d.FullName -Directory|Where-Object Name -Match 'runner$'){foreach($n in @('bin','obj')){$p=Join-Path $runner.FullName $n;if(Test-Path $p){$targets+=$p}}}}}
 $entries=@()
 foreach($p in $targets|Sort-Object -Unique){
  $full=[IO.Path]::GetFullPath($p);if(-not $full.StartsWith($art+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Outside artifacts'}
  $item=Get-Item -LiteralPath $full -Force;$cursor=$item
  while($cursor){if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse ancestor: $full"};if($cursor.FullName -eq $root){break};$cursor=$cursor.Parent}
  $content=@(Get-ChildItem -LiteralPath $full -Recurse -Force)
  if(@($content|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Nested reparse point'}
  $entries+=[pscustomobject]@{path=$full;bytes=[long](($content|Where-Object {-not $_.PSIsContainer}|Measure-Object Length -Sum).Sum)}
 }
 $report=[ordered]@{status='VALIDATED';startedKst='2026-10-24T08:19:04+09:00';beforeBytes=$before;targets=$entries;deletedBytes=0;preserved='Current test sources/projects/captures/receipts; current common-engine cache; fixed runner; offline package caches; latest and fallback releases; user original'}
 $receipt=Join-Path $art 'cleanup-retained-tests-20261024.json'
 $report|ConvertTo-Json -Depth 6|Set-Content $receipt -Encoding utf8
 foreach($e in $entries){Remove-Item -LiteralPath $e.path -Recurse -Force;if(Test-Path -LiteralPath $e.path){throw 'Deletion incomplete'};$report.deletedBytes+=$e.bytes}
 foreach($p in $hashes.Keys){if((Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $hashes[$p]){throw 'Release changed'}}
 $report.afterBytes=[long]((Get-ChildItem $root -Recurse -File -Force|Measure-Object Length -Sum).Sum)
 $report.releaseFilesHashVerified=$hashes.Count;$report.status='PASS';$report.completedKst=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(9)).ToString('o')
 $report|ConvertTo-Json -Depth 6|Set-Content $receipt -Encoding utf8
 [pscustomobject]$report|Select-Object status,beforeBytes,afterBytes,deletedBytes,releaseFilesHashVerified,completedKst|ConvertTo-Json
} finally {$lease.Dispose()}


