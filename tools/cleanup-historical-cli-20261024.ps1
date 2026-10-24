$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath('C:\Projects\EngineMigrator')
$art=Join-Path $root 'artifacts'
$latest='EngineMigrator-fixed-spawn-preview-20261024'
$fallback='EngineMigrator-start-chain-preview-20261024'
$before=[long]((Get-ChildItem -LiteralPath $root -Recurse -File -Force | Measure-Object Length -Sum).Sum)
$targets=@()
# Historical published command-line builds. Keep receipts, logs and test fixtures.
foreach($dir in (Get-ChildItem -LiteralPath $art -Directory | Where-Object { $_.Name -match '(^|-)cli($|-)' -or $_.Name -in @('actor-release','script-blueprint-release') })) {
 foreach($file in (Get-ChildItem -LiteralPath $dir.FullName -Recurse -File -Force)) {
  if($file.Extension -in @('.dll','.exe','.pdb') -or $file.Name -match '\.(deps|runtimeconfig)\.json$') { $targets += $file.FullName }
 }
}
foreach($item in (Get-ChildItem -LiteralPath $art -Force | Where-Object { $_.Name -like 'EngineMigrator*' -and $_.Name -notin @($latest,$fallback,($latest+'.zip'),($fallback+'.zip')) })) {
 if($item.Name -match 'preview|actor-preview|resource-preview|script-blueprint-preview') { $targets += $item.FullName }
}
$records=@()
foreach($path in ($targets | Sort-Object -Unique)) {
 $full=[IO.Path]::GetFullPath($path)
 if(-not $full.StartsWith($art+'\',[StringComparison]::OrdinalIgnoreCase)){throw "Outside artifacts: $full"}
 $item=Get-Item -LiteralPath $full -Force
 $cursor=$item
 while($cursor){if($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw "Reparse: $full"};if($cursor.FullName -eq $root){break};$cursor=Get-Item -LiteralPath (Split-Path -Parent $cursor.FullName) -Force}
 $entries=if($item.PSIsContainer){@(Get-ChildItem -LiteralPath $full -Recurse -Force)}else{@($item)}
 if(@($entries | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw "Nested reparse: $full"}
 $bytes=[long](($entries | Where-Object {-not $_.PSIsContainer} | Measure-Object Length -Sum).Sum)
 $records += [pscustomobject]@{path=$full;bytes=$bytes}
}
$report=[ordered]@{startedKst=[DateTimeOffset]::Now.ToOffset([TimeSpan]::FromHours(9)).ToString('o');status='VALIDATED';beforeBytes=$before;latest=$latest;fallback=$fallback;targets=$records;deletedBytes=0;preserved='Active engine projects, all test sources/captures/receipts, fixed runtime runner, Unity package cache and source project'}
$manifest=Join-Path $art 'cleanup-20261024.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
foreach($entry in $records){Remove-Item -LiteralPath $entry.path -Force -Recurse;if(Test-Path -LiteralPath $entry.path){throw "Incomplete: $($entry.path)"};$report.deletedBytes += $entry.bytes}
$report.afterBytes=[long]((Get-ChildItem -LiteralPath $root -Recurse -File -Force | Measure-Object Length -Sum).Sum)
$report.status='PASS'
$report.completedKst=[DateTimeOffset]::Now.ToOffset([TimeSpan]::FromHours(9)).ToString('o')
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
[pscustomobject]@{Status=$report.status;DeletedFilesOrFolders=$records.Count;BeforeGiB=$before/1GB;DeletedGiB=$report.deletedBytes/1GB;AfterGiB=$report.afterBytes/1GB;CompletedKst=$report.completedKst} | ConvertTo-Json


