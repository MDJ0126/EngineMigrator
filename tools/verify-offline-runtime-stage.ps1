$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
$root=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/offline-stage-fixture-'+[guid]::NewGuid().ToString('N'))
$checks=0
function Check([bool]$Value,[string]$Name){if(!$Value){throw $Name};$script:checks++;Write-Output ('PASS '+$Name)}
foreach($name in @('First','Second')){
    $source=Join-Path $root $name
    New-Item -ItemType Directory -Path (Join-Path $source 'RuntimeWorld/Binaries/Win64') -Force|Out-Null
    New-Item -ItemType Directory -Path (Join-Path $source 'RuntimeWorld/Content') -Force|Out-Null
    Set-Content (Join-Path $source 'RuntimeWorld/Binaries/Win64/RuntimeWorld.exe') ('TEST DATA ONLY: '+$name)
    Set-Content (Join-Path $source ('RuntimeWorld/Content/'+$name+'.txt')) $name
}
$first=New-OfflineRuntimeStage -SourceWindows (Join-Path $root 'First')
try{
    Check ($first.Manifest.files.Count-eq 2) 'First snapshot contains exactly the source files'
    Check ((Get-FileHash $first.Executable).Hash-eq(Get-FileHash (Join-Path $root 'First/RuntimeWorld/Binaries/Win64/RuntimeWorld.exe')).Hash) 'Stable executable bytes match source test data'
    $blocked=$false;try{$unexpected=New-OfflineRuntimeStage -SourceWindows (Join-Path $root 'Second');$unexpected.Lease.Dispose()}catch [IO.IOException]{$blocked=$true}
    Check $blocked 'Concurrent staging fails before overwriting the leased runtime'
}finally{$first.Lease.Dispose()}
$second=New-OfflineRuntimeStage -SourceWindows (Join-Path $root 'Second')
try{
    $content=Join-Path (Split-Path (Split-Path (Split-Path $second.Executable -Parent) -Parent) -Parent) 'Content'
    Check (!(Test-Path (Join-Path $content 'First.txt'))-and(Test-Path (Join-Path $content 'Second.txt'))) 'Replacing a snapshot removes stale content'
    Check ((Get-Content $second.Executable -Raw)-like '*Second*') 'Second snapshot replaces the executable at the same stable path'
    Check ((Get-Content (Join-Path $root 'First/RuntimeWorld/Binaries/Win64/RuntimeWorld.exe') -Raw)-like '*First*') 'Original package sources remain unchanged'
}finally{$second.Lease.Dispose()}
[pscustomobject]@{status='PASS';checks=$checks;testDataOnly=$true;engineStarted=$false;fixture=$root}|ConvertTo-Json|Set-Content (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/offline-runtime-stage-verification.json')
