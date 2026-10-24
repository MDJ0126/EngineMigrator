$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath('C:\Projects\EngineMigrator')
$art=Join-Path $root 'artifacts'
$latest='EngineMigrator-fixed-spawn-preview-20261024'
$fallback='EngineMigrator-start-chain-preview-20261024'
$before=[long]((Get-ChildItem -LiteralPath $root -Recurse -File -Force | Measure-Object Length -Sum).Sum)
$targets=@()
$running=@(Get-Process EngineMigrator.App -ErrorAction SilentlyContinue)
foreach($d in Get-ChildItem -LiteralPath $art -Directory){
 if($d.Name -like 'EngineMigrator-*-preview-*' -and $d.Name -notin @($latest,$fallback)){
  if(@($running | Where-Object {-not $_.Path -or $_.Path.StartsWith($d.FullName+'\',[StringComparison]::OrdinalIgnoreCase)}).Count){throw 'Old release is running or path cannot be confirmed'}
  $targets+=$d.FullName
  if(Test-Path -LiteralPath ($d.FullName+'.zip')){$targets+=($d.FullName+'.zip')}
 }
}
# Remove compiled historical harness copies, retaining their source, captures and receipts.
$keepHarness=@('fixed-spawn-check','fixed-project-check','start-chain-check','integrated-project-check','scaled-prefab-check','core-engine-launch-tests','ui-equality-check')
foreach($d in Get-ChildItem -LiteralPath $art -Directory){
 if($d.Name -notin $keepHarness -and $d.Name -match '-check$'){
  foreach($runner in Get-ChildItem -LiteralPath $d.FullName -Directory | Where-Object Name -match 'runner$'){
   foreach($n in @('bin','obj')){ $p=Join-Path $runner.FullName $n;if(Test-Path -LiteralPath $p){$targets+=$p}}
  }
 }
}
# Publication already verified; these compiler intermediates can be regenerated.
foreach($project in Get-ChildItem -LiteralPath (Join-Path $root 'src') -Directory){foreach($n in @('bin','obj')){$p=Join-Path $project.FullName $n;if(Test-Path -LiteralPath $p){$targets+=$p}}}
$proof=Get-Content -LiteralPath (Join-Path $art 'fixed-spawn-archive-verification.json') -Raw | ConvertFrom-Json
if($proof.status -ne 'PASS'){throw 'Release proof missing'}
if(Test-Path -LiteralPath $proof.unicodeExtraction){$targets+=$proof.unicodeExtraction}
$records=@()
foreach($p in $targets | Sort-Object -Unique){
 $full=[IO.Path]::GetFullPath($p)
 if(-not $full.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Outside workspace: $full"}
 if($full.StartsWith((Join-Path $art $latest)+'\') -or $full -eq (Join-Path $art $latest)){throw 'Latest protected'}
 $item=Get-Item -LiteralPath $full -Force
 $cursor=$item
 while($cursor){if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse ancestor: $full"};if($cursor.FullName -eq $root){break};$cursor=Get-Item -LiteralPath (Split-Path -Parent $cursor.FullName) -Force}
 $entries=if($item.PSIsContainer){@(Get-ChildItem -LiteralPath $full -Recurse -Force)}else{@($item)}
 if(@($entries | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw "Nested reparse: $full"}
 $records+=[pscustomobject]@{path=$full;bytes=[long](($entries | Where-Object {-not $_.PSIsContainer} | Measure-Object Length -Sum).Sum)}
}
$manifest=Join-Path $art 'cleanup-user-request-20261024.json'
$report=[ordered]@{status='VALIDATED';startedKst='2026-10-24T06:27:34+09:00';beforeBytes=$before;targets=$records;deletedBytes=0;latest=$latest;fallback=$fallback;preserved='Current engine project copies, test sources/captures/receipts, current and fallback release, fixed runner, original project, current tests and harnesses'}
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
foreach($r in $records){Remove-Item -LiteralPath $r.path -Recurse -Force;if(Test-Path -LiteralPath $r.path){throw "Incomplete deletion: $($r.path)"};$report.deletedBytes+=$r.bytes}
$proof | Add-Member -NotePropertyName temporaryExtractionRemoved -NotePropertyValue $true -Force
$proof | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $art 'fixed-spawn-archive-verification.json') -Encoding utf8
$report.afterBytes=[long]((Get-ChildItem -LiteralPath $root -Recurse -File -Force | Measure-Object Length -Sum).Sum)
$report.status='PASS';$report.completedKst=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(9)).ToString('o')
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
$report | Select-Object status,beforeBytes,afterBytes,deletedBytes,completedKst | ConvertTo-Json


