$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'));$art=Join-Path $root 'artifacts'
$proofPath=Join-Path $art 'start-chain-archive-verification.json'
$proof=Get-Content -LiteralPath $proofPath -Raw|ConvertFrom-Json
if($proof.status -ne 'PASS'){throw 'Verified current release required'}
$lease=[IO.File]::Open((Join-Path $art 'offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
 $latest=Join-Path $art 'EngineMigrator-start-chain-preview-20261024'
 $fallback=Join-Path $art 'EngineMigrator-integrated-project-preview-20261024'
 $world=Join-Path $art 'start-chain-check/world49/Project'
 $targets=@($proof.unicodeExtraction)+@(Get-ChildItem -LiteralPath $world -Recurse -Directory -Filter Intermediate|Select-Object -ExpandProperty FullName)
 $running=@(Get-Process EngineMigrator.App -ErrorAction SilentlyContinue);$preservedRunning=@()
 foreach($p in @((Join-Path $art 'EngineMigrator-phase-chain-preview-20261024'),(Join-Path $root 'src/EngineMigrator.App/bin'))){
  if(Test-Path -LiteralPath $p){if(@($running|Where-Object {-not $_.Path -or $_.Path.StartsWith($p+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){$preservedRunning+=$p}else{$targets+=$p;if(Test-Path -LiteralPath ($p+'.zip')){$targets+=($p+'.zip')}}}
 }
 $inputs=Get-Content (Join-Path $art 'start-chain-check/world49/build-hashes.json') -Raw|ConvertFrom-Json
 $records=@()
 foreach($p in $targets|Sort-Object -Unique){
  if(-not(Test-Path -LiteralPath $p)){continue};$full=[IO.Path]::GetFullPath($p)
  if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Outside workspace'}
  foreach($protected in @($latest,$fallback)){if($full -eq $protected -or $full.StartsWith($protected+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Release protected'}}
  foreach($inputFile in $inputs.PSObject.Properties.Name){if($inputFile.StartsWith($full+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Verification input protected: $inputFile"}}
  $item=Get-Item -LiteralPath $full -Force;$cursor=$item
  while($cursor){if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Reparse ancestor'};if($cursor.FullName -eq $root){break};$cursor=$cursor.Parent}
  $content=if($item.PSIsContainer){@(Get-ChildItem -LiteralPath $full -Recurse -Force)}else{@($item)}
  if(@($content|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Nested reparse point'}
  $records+=[pscustomobject]@{path=$full;bytes=[long](($content|Where-Object {-not $_.PSIsContainer}|Measure-Object Length -Sum).Sum)}
 }
 $report=[ordered]@{status='VALIDATED';targets=$records;deletedBytes=0;preservedRunning=$preservedRunning;preserved='Latest and fallback releases; current common engine and Unity caches; all original/native/restored sources, binaries, captures, receipts and offline package caches'}
 $path=Join-Path $art 'start-chain-check/cleanup.json';$report|ConvertTo-Json -Depth 6|Set-Content $path -Encoding utf8
 foreach($entry in $records){Remove-Item -LiteralPath $entry.path -Recurse -Force;if(Test-Path -LiteralPath $entry.path){throw 'Incomplete deletion'};$report.deletedBytes+=$entry.bytes}
 $proof|Add-Member -NotePropertyName temporaryExtractionRemoved -NotePropertyValue $true -Force
 $proof|ConvertTo-Json -Depth 6|Set-Content $proofPath -Encoding utf8
 $report.status='PASS';$report.completedKst=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(9)).ToString('o')
 $report|ConvertTo-Json -Depth 6|Set-Content $path -Encoding utf8
 [pscustomobject]$report|Select-Object status,deletedBytes,preservedRunning,completedKst|ConvertTo-Json
}finally{$lease.Dispose()}
