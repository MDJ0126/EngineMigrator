$ErrorActionPreference='Stop'
$env:EM_ENGINE_AUDIT_PARENT='98765'
$audit=Join-Path $PSScriptRoot '../src/EngineMigrator.Core/EngineUnityIpcAudit.ps1'
function Get-CimInstance {param($ClassName,$Filter)
 if($Filter-like'ParentProcessId=*'){return [pscustomobject]@{ProcessId=12345;ParentProcessId=98765;Name='UnityPackageManager.exe';CommandLine=$global:emIpcTest_firstCommand}}
 return $global:emIpcTest_second
}
function Get-NetTCPConnection {param($State,$ErrorAction) return $global:emIpcTest_listeners}
$checks=0
foreach($case in @('exited','live-unknown','live-ipc','live-tcp','wrong-parent','wrong-transport')){
 $global:emIpcTest_firstCommand=$null;$global:emIpcTest_listeners=@();$global:emIpcTest_second=@()
 if($case-ne'exited'){
  $line=if($case-eq'live-unknown'){$null}elseif($case-eq'wrong-transport'){'upm -port 123'}else{'upm -ipc Pipe'}
  $parent=if($case-eq'wrong-parent'){111}else{98765}
  $global:emIpcTest_second=@([pscustomobject]@{ProcessId=12345;ParentProcessId=$parent;Name='UnityPackageManager.exe';CommandLine=$line})
 }
 if($case-eq'live-tcp'){$global:emIpcTest_listeners=@([pscustomobject]@{OwningProcess=12345})}
 $failed=$false;$result=$null
 try{$result=(& $audit)|ConvertFrom-Json}catch{$failed=$true;if($case-in@('exited','live-ipc')){Write-Output $_}}
 $expectedFailure=$case-in@('live-unknown','live-tcp','wrong-parent','wrong-transport')
 if($failed-ne$expectedFailure){throw "Unexpected audit outcome: $case"}
 if($case-eq'exited'-and(@($result.exitedProcessIds).Count-ne1-or@($result.rows).Count-ne0)){throw 'Missing confirmed exit evidence'}
 if($case-eq'live-ipc'-and(@($result.rows).Count-ne1-or$result.rows[0].tcpListenerCount-ne0)){throw 'Missing live IPC evidence'}
 $checks++;Write-Output "PASS $case"
}
Write-Output "$checks IPC audit checks passed. Mock process observations; real engine verification remains separate."
