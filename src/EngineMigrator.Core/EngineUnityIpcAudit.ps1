[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$ErrorActionPreference='Stop'
$parentId=[int]$env:EM_ENGINE_AUDIT_PARENT
if($parentId-le0){throw 'Valid owned Unity process ID required'}
$rows=@()
$exitedProcessIds=@()
foreach($child in @(Get-CimInstance Win32_Process -Filter ('ParentProcessId='+$parentId)|Where-Object {$_.Name-eq'UnityPackageManager.exe'})){
 if(!$child.CommandLine){
  $confirmed=@(Get-CimInstance Win32_Process -Filter ('ProcessId='+$child.ProcessId))
  if(!$confirmed.Count){$exitedProcessIds+=[int]$child.ProcessId;continue}
  if($confirmed.Count-ne1-or$confirmed[0].ParentProcessId-ne$parentId-or$confirmed[0].Name-ne'UnityPackageManager.exe'-or!$confirmed[0].CommandLine){throw 'Own package manager command line could not be audited'}
  $child=$confirmed[0]
 }
 if($child.CommandLine-match'(?i)(?:^|[\s"])(?:-v|--version)(?:[\s"]|$)'){continue}
 if($child.CommandLine-notmatch'(?i)(?:^|[\s"])-ipc(?:[\s"]|$)'){throw 'Own package manager is not using audited named IPC'}
 $listeners=@(Get-NetTCPConnection -State Listen -ErrorAction Stop|Where-Object {$_.OwningProcess-eq$child.ProcessId})
 if($listeners.Count){throw 'Unexpected TCP listener in own IPC package manager'}
 $rows+=[pscustomobject]@{pid=$child.ProcessId;transport='Named IPC pipe';tcpListenerCount=0}
}
@{rows=$rows;exitedProcessIds=$exitedProcessIds;policyChanged=$false}|ConvertTo-Json -Depth 4 -Compress
