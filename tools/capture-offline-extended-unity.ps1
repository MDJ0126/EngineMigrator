param([Parameter(Mandatory=$true)][string]$Project,[Parameter(Mandatory=$true)][string]$Scene,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
. (Join-Path $PSScriptRoot 'Assert-SharedEditorFirewall.ps1')
$Project=Assert-OfflineRuntimePath $Project;$Output=Assert-OfflineRuntimePath $Output
if(Test-Path -LiteralPath $Output){throw 'Use a fresh private output directory'}
if($Scene-notmatch'^Assets/[A-Za-z0-9_/.-]+\.unity$'-or!(Test-Path -LiteralPath (Join-Path $Project $Scene))){throw 'A private existing Unity scene is required'}
$exe='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe'
$preflight=Assert-SharedEditorFirewall -Executable $exe
$lease=[IO.File]::Open((Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$settings=Join-Path $Project 'ProjectSettings/EditorSettings.asset';$originalSettings=[IO.File]::ReadAllBytes($settings)
try{
 New-Item -ItemType Directory -Path $Output|Out-Null
 $template=Get-Content (Join-Path $PSScriptRoot 'verify-extended-runtime.ps1') -Raw
 $begin=$template.IndexOf("@'")+2;$end=$template.IndexOf("'@|Set-Content",$begin)
 if($begin-lt 2-or$end-lt$begin){throw 'Known extended runtime template not found'}
 Set-Content (Join-Path $Project 'Assets/Editor/EMExtendedRuntime.cs') $template.Substring($begin,$end-$begin)
 $package=Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da/Transfer/units/s_99c9720ab356a0642a771bea13969a05/package.json'
 Copy-Item -LiteralPath $package -Destination (Join-Path $Project 'extended-source-package.json') -Force
 $capture=Join-Path $Project 'extended-runtime.json';if(Test-Path -LiteralPath $capture){Copy-Item -LiteralPath $capture -Destination (Join-Path $Output 'previous-capture.json')}
 $start=[Diagnostics.ProcessStartInfo]::new($exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.Environment['EM_VERIFY_FRAMES']='250';$start.Environment['EM_VERIFY_SCENE']=$Scene
 foreach($argument in @('-batchmode','-nographics','-projectPath',$Project,'-executeMethod','EMExtendedRuntime.Run','-logFile',(Join-Path $Output 'unity.log'))){$start.ArgumentList.Add($argument)}
 $started=Get-Date;$process=[Diagnostics.Process]::Start($start);$elapsed=[Diagnostics.Stopwatch]::StartNew()
 $upmEvidence=@{}
 while(!$process.WaitForExit(1000)){
  foreach($child in @(Get-CimInstance Win32_Process -Filter ('ParentProcessId='+$process.Id)|Where-Object {$_.Name-eq'UnityPackageManager.exe'})){
   $child|Select-Object ProcessId,Name,CommandLine|ConvertTo-Json|Set-Content (Join-Path $Output 'upm-last-observed.json')
   if(!$child.CommandLine){continue}
   if($child.CommandLine-match'(?i)(?:^|[\s"])(?:-v|--version)(?:[\s"]|$)'){continue}
   if($child.CommandLine-notmatch'(?i)(?:^|[\s"])-ipc(?:[\s"]|$)'){$process.Kill($true);$process.WaitForExit();throw 'Own package manager did not use the audited IPC transport'}
   $listeners=@(Get-NetTCPConnection -State Listen -OwningProcess $child.ProcessId -ErrorAction SilentlyContinue);if($listeners.Count){$process.Kill($true);$process.WaitForExit();throw 'Unexpected TCP listener in own IPC package manager'}
   $upmEvidence[[string]$child.ProcessId]=[pscustomobject]@{pid=$child.ProcessId;commandLine=$child.CommandLine;tcpListenerCount=0;transport='Named IPC pipe'}
  }
  if($elapsed.Elapsed.TotalSeconds-gt300-or(Get-PSDrive C).Free-lt300000000){$process.Kill($true);$process.WaitForExit();throw 'Own Unity verification exceeded time/disk limit'}}
 [void](Assert-SharedEditorFirewall -Executable $exe)
 if($process.ExitCode-ne 0-or!(Test-Path -LiteralPath $capture)-or(Get-Item -LiteralPath $capture).LastWriteTime-lt$started){throw 'Unity did not produce a fresh successful capture'}
 Copy-Item -LiteralPath $capture -Destination (Join-Path $Output 'unity-execution.json')
 $data=Get-Content (Join-Path $Output 'unity-execution.json') -Raw|ConvertFrom-Json
 if(@($data.frames).Count-ne250-or@($data.deltas).Count-ne250){throw 'Actual Unity frame count differs'}
 @{status='CAPTURED';frames=250;pid=$process.Id;project=$Project;scene=$Scene;noUpm=$false;upmTransport='Named IPC pipe';firewallPolicyChanged=$false;timestamp=(Get-Date).ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $Output 'execution.json')
 @($upmEvidence.Values)|ConvertTo-Json -Depth 4|Set-Content (Join-Path $Output 'upm-ipc-evidence.json')
 Write-Output 'AUDITED_UNITY_250_FRAMES_CAPTURED'
}finally{[IO.File]::WriteAllBytes($settings,$originalSettings);$lease.Dispose()}
