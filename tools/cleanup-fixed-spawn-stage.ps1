$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));$art=Join-Path $root 'artifacts';$h=Join-Path $art 'fixed-project-check'
$proofPath=Join-Path $art 'fixed-spawn-archive-verification.json';$proof=Get-Content $proofPath -Raw|ConvertFrom-Json
if($proof.status -ne 'PASS'){throw 'Verified release required'}
$lease=[IO.File]::Open((Join-Path $art 'offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
 $forward=Get-Content (Join-Path $h 'forward-result.json') -Raw|ConvertFrom-Json;$reverse=Get-Content (Join-Path $h 'reverse-result.json') -Raw|ConvertFrom-Json
 $inputs=Get-Content (Join-Path $h 'source-verification.json') -Raw|ConvertFrom-Json
 $targets=@($proof.unicodeExtraction,(Join-Path $forward.SourceCopy 'Library'),(Join-Path $h 'UnityTemplate/Library'),(Join-Path $forward.TargetCopy 'Intermediate'))
 foreach($project in @((Join-Path $h 'UnityInput'),$reverse.TargetCopy)){$library=Join-Path $project 'Library';if(Test-Path -LiteralPath $library){foreach($d in Get-ChildItem -LiteralPath $library -Directory -Force){if($d.Name -notin @('PackageCache','PackageManager')){$targets+=$d.FullName}}}}
 $running=@(Get-Process EngineMigrator.App -ErrorAction SilentlyContinue);$preservedRunning=@()
 foreach($p in @((Join-Path $art 'EngineMigrator-integrated-project-preview-20261024'),(Join-Path $root 'src/EngineMigrator.App/bin'))){if(Test-Path -LiteralPath $p){if(@($running|Where-Object{-not $_.Path -or $_.Path.StartsWith($p+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){$preservedRunning+=$p}else{$targets+=$p;if(Test-Path -LiteralPath ($p+'.zip')){$targets+=($p+'.zip')}}}}
 $records=@()
 foreach($p in $targets|Sort-Object -Unique){
  if(-not(Test-Path -LiteralPath $p)){continue};$full=[IO.Path]::GetFullPath($p)
  if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Outside workspace'}
  foreach($name in @('EngineMigrator-fixed-spawn-preview-20261024','EngineMigrator-start-chain-preview-20261024')){$protected=Join-Path $art $name;if($full -eq $protected -or $full.StartsWith($protected+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Protected release'}}
  foreach($inputFile in $inputs.files.PSObject.Properties.Name){if($inputFile.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Verification input protected: $inputFile"}}
  $item=Get-Item -LiteralPath $full -Force;$cursor=$item;while($cursor){if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse ancestor'};if($cursor.FullName -eq $root){break};$cursor=$cursor.Parent}
  $content=if($item.PSIsContainer){@(Get-ChildItem -LiteralPath $full -Recurse -Force)}else{@($item)}
  if(@($content|Where-Object{$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Nested reparse point'}
  $records+=[pscustomobject]@{path=$full;bytes=[long](($content|Where-Object{-not $_.PSIsContainer}|Measure-Object Length -Sum).Sum)}
 }
 $report=[ordered]@{status='VALIDATED';targets=$records;deletedBytes=0;preservedRunning=$preservedRunning;preserved='Current common-engine and Unity caches; new/fallback releases; all source projects, generated verification inputs, native binaries, offline package caches, transfer packages, captures and receipts'}
 $receipt=Join-Path $art 'fixed-spawn-check/cleanup.json';$report|ConvertTo-Json -Depth 6|Set-Content $receipt -Encoding utf8
 foreach($e in $records){Remove-Item -LiteralPath $e.path -Recurse -Force;if(Test-Path -LiteralPath $e.path){throw 'Deletion incomplete'};$report.deletedBytes+=$e.bytes}
 $proof|Add-Member -NotePropertyName temporaryExtractionRemoved -NotePropertyValue $true -Force;$proof|ConvertTo-Json -Depth 6|Set-Content $proofPath -Encoding utf8
 $report.status='PASS';$report.completedKst=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(9)).ToString('o');$report|ConvertTo-Json -Depth 6|Set-Content $receipt -Encoding utf8
 [pscustomobject]$report|Select-Object status,deletedBytes,preservedRunning,completedKst|ConvertTo-Json
}finally{$lease.Dispose()}
