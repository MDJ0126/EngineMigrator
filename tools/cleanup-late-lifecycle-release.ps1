$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$art=Join-Path $root 'artifacts'
$proofPath=Join-Path $art 'late-lifecycle-archive-verification.json'
$proof=Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
if($proof.status -ne 'PASS'){throw 'Release archive proof is required'}
$lease=[IO.File]::Open((Join-Path $art 'offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
 $targets=@($proof.unicodeExtraction)
 $old=Join-Path $art 'EngineMigrator-scaled-prefab-preview-20261024'
 $running=@(Get-Process EngineMigrator.App -ErrorAction SilentlyContinue)
 $preservedRunning=@()
 if(Test-Path -LiteralPath $old){
  if(@($running|Where-Object{-not $_.Path -or $_.Path.StartsWith($old+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){$preservedRunning+=$old}
  else{$targets+=$old;if(Test-Path -LiteralPath ($old+'.zip')){$targets+=($old+'.zip')}}
 }
 foreach($harnessName in @('late-mutation-check','late-update-check')){foreach($name in @('bin','obj')){$p=Join-Path $art "$harnessName/runner/$name";if(Test-Path -LiteralPath $p){$targets+=$p}}}
 $entries=@()
 foreach($p in $targets){
  $full=[IO.Path]::GetFullPath($p)
  if(-not $full.StartsWith($art+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Outside artifacts: $full"}
  $item=Get-Item -LiteralPath $full -Force;$cursor=$item
  while($cursor){if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse ancestor: $full"};if($cursor.FullName -eq $root){break};$cursor=Get-Item -LiteralPath (Split-Path -Parent $cursor.FullName) -Force}
  $contents=if($item.PSIsContainer){@(Get-ChildItem -LiteralPath $full -Recurse -Force)}else{@($item)}
  if(@($contents|Where-Object{$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw "Nested reparse: $full"}
  $entries+=[pscustomobject]@{path=$full;bytes=[long](($contents|Where-Object{-not $_.PSIsContainer}|Measure-Object Length -Sum).Sum)}
 }
 $report=[ordered]@{status='VALIDATED';targets=$entries;deletedBytes=0;preservedRunning=$preservedRunning;retainedReleases=@('EngineMigrator-late-lifecycle-preview-20261024','EngineMigrator-late-update-preview-20261024')}
 $manifest=Join-Path $art 'late-lifecycle-release-cleanup.json';$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $manifest -Encoding utf8
 foreach($entry in $entries){Remove-Item -LiteralPath $entry.path -Recurse -Force;if(Test-Path -LiteralPath $entry.path){throw "Incomplete deletion: $($entry.path)"};$report.deletedBytes+=$entry.bytes}
 $proof|Add-Member -NotePropertyName temporaryExtractionRemoved -NotePropertyValue $true -Force
 $proof|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $proofPath -Encoding utf8
 $report.status='PASS';$report.completedKst=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(9)).ToString('o');$report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $manifest -Encoding utf8
 Write-Output ('RELEASE_CLEANUP_PASS removedGiB={0:N3} targets={1}'-f($report.deletedBytes/1GB),$entries.Count)
}finally{$lease.Dispose()}

