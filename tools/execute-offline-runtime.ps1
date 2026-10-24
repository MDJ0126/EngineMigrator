param([Parameter(Mandatory=$true)][string]$SourceWindows,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
$Output=Assert-OfflineRuntimePath $Output
New-Item -ItemType Directory -Path $Output -Force|Out-Null
$stage=New-OfflineRuntimeStage -SourceWindows $SourceWindows
try{
    $rule=Assert-PrivateRuntimeFirewall -Executable $stage.Executable
    $capture=Join-Path $Output ('execution-'+[guid]::NewGuid().ToString('N')+'.json')
    $start=[Diagnostics.ProcessStartInfo]::new($stage.Executable)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WorkingDirectory=Split-Path $stage.Executable -Parent
    foreach($argument in @('-nullrhi','-RenderOffscreen','-nosound','-unattended','-notraceserver','-UDPMESSAGING_TRANSPORT_ENABLE=0','-UseFixedTimeStep','-FPS=50',('-EMRuntimeProbe='+$capture),('-abslog='+$capture+'.log'))){$start.ArgumentList.Add($argument)}
    $process=[Diagnostics.Process]::Start($start)
    if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Own fixed-path runtime timed out'}
    if($process.ExitCode-ne 0-or!(Test-Path -LiteralPath $capture)){throw 'Fixed-path runtime failed to produce fresh capture'}
    [void](Assert-PrivateRuntimeFirewall -Executable $stage.Executable)
    $result=[pscustomobject]@{capture=$capture;executable=$stage.Executable;executableSha256=$stage.Manifest.executableSha256;sourceWindows=$stage.Manifest.sourceWindows;rule=$rule;exitCode=$process.ExitCode;pid=$process.Id;timestamp=(Get-Date).ToString('o');packagedExecutableExecuted=$true}
    $result|ConvertTo-Json|Set-Content (Join-Path $Output 'fixed-path-execution.json')
    return $result
}finally{$stage.Lease.Dispose()}
