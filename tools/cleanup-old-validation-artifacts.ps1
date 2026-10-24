$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$art = Join-Path $root 'artifacts'
$latest = 'EngineMigrator-fixed-spawn-preview-20261024'
$names = @(Get-ChildItem -LiteralPath $art -Directory | Where-Object {
    ($_.Name -like 'EngineMigrator-*-preview-*' -and $_.Name -notin @($latest,'EngineMigrator-start-chain-preview-20261024')) -or
    ($_.Name -like 'runtime-*-packaging*' -and $_.Name -ne 'runtime-ui-api-packaging-v2') -or
    $_.Name -in @('배포 검증','배포 실행 검증','배포검증-확장프리뷰','packaging-tests')
} | Select-Object -ExpandProperty Name)
$targets = @()
foreach ($name in $names) {
    $targets += Join-Path $art $name
    $zip = Join-Path $art ($name + '.zip')
    if (Test-Path -LiteralPath $zip -PathType Leaf) { $targets += $zip }
}
# The engine regression pipeline has finished. Never touch its retained project,
# Unity fixtures, current release, source, or fixed offline execution directory.
$protected = @('ui-equality-check','core-engine-launch-tests','runtime-ui-api-forward-v2',
 'runtime-ui-api-tests','runtime-ui-api-packaging-v2','user-project-baseline',
 'offline-runtime-runner','.retained-unity-packages',$latest,'EngineMigrator-start-chain-preview-20261024')
$records = @()
foreach ($target in $targets) {
    $full = [IO.Path]::GetFullPath($target)
    if (-not $full.StartsWith($art + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw "Outside artifacts: $full" }
    if ((Split-Path -Leaf $full) -in $protected) { throw "Protected target: $full" }
    $item = Get-Item -LiteralPath $full -Force
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse target: $full" }
    $cursor = Get-Item -LiteralPath (Split-Path -Parent $full) -Force
    while ($cursor) {
        if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse ancestor: $($cursor.FullName)" }
        if ($cursor.FullName -eq $root) { break }
        $cursor = $cursor.Parent
    }
    $files = if ($item.PSIsContainer) { @(Get-ChildItem -LiteralPath $full -Recurse -Force) } else { @($item) }
    if (@($files | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) { throw "Nested reparse target: $full" }
    $bytes = [long](($files | Where-Object { -not $_.PSIsContainer } | Measure-Object -Property Length -Sum).Sum)
    $records += [pscustomobject]@{path=$full;bytes=$bytes}
}
$manifest = Join-Path $art 'cleanup-20261023.json'
$record = [ordered]@{startedKst=[DateTimeOffset]::Now.ToOffset([TimeSpan]::FromHours(9)).ToString('o');status='VALIDATED';protected=$protected;targets=$records;deletedBytes=0}
$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
foreach ($entry in $records) {
    Remove-Item -LiteralPath $entry.path -Recurse -Force
    if (Test-Path -LiteralPath $entry.path) { throw "Deletion incomplete: $($entry.path)" }
    $record.deletedBytes += $entry.bytes
    $record.status = 'DELETING'
    $record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
}
$record.status='PASS'
$record.completedKst=[DateTimeOffset]::Now.ToOffset([TimeSpan]::FromHours(9)).ToString('o')
$record | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
Write-Output ('CLEANUP_PASS targets={0} reclaimedGiB={1:N2}' -f $records.Count,($record.deletedBytes/1GB))


