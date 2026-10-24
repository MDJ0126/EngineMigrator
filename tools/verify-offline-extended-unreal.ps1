param([Parameter(Mandatory=$true)][string]$UnityCapture,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
. (Join-Path $PSScriptRoot 'Assert-SharedEditorFirewall.ps1')
$UnityCapture=Assert-OfflineRuntimePath $UnityCapture;$Output=Assert-OfflineRuntimePath $Output
if(Test-Path -LiteralPath $Output){throw 'A fresh native verification directory is required'}
$data=Get-Content $UnityCapture -Raw|ConvertFrom-Json
if(@($data.frames).Count-ne250-or@($data.deltas).Count-ne250){throw 'Actual 250-frame Unity capture required'}
$root=Split-Path $PSScriptRoot -Parent;$run=Join-Path $root 'artifacts/runtime-ui-api-forward-v2/PROJECT-da11d7b180da';$target=Join-Path $run 'Target'
$unit='s_99c9720ab356a0642a771bea13969a05';$package=Join-Path $run ('Transfer/units/'+$unit+'/package.json')
$manifest=Get-Content (Join-Path $target '.EngineMigratorProject.json') -Raw|ConvertFrom-Json;$selected=@($manifest.units|Where-Object {$_.id-eq$unit-and$_.kind-eq'scene'})
if($selected.Count-ne1){throw 'Legacy scene unit missing'}
$exe='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe';$preflight=Assert-SharedEditorFirewall -Executable $exe
$lease=[IO.File]::Open((Join-Path $root 'artifacts/offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
 New-Item -ItemType Directory -Path $Output|Out-Null
 @{deltas=$data.deltas;endPlayOnly=$true;clicks=@($data.clicks)}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $Output 'input.json')
 @{map=$selected[0].map;input=(Join-Path $Output 'input.json');output=$Output;actorNames=@{}}|ConvertTo-Json|Set-Content (Join-Path $target 'SceneVerify.json')
 $start=[Diagnostics.ProcessStartInfo]::new($exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 foreach($argument in @((Join-Path $target 'RuntimeWorld.uproject'),'-run=pythonscript',('-script='+$root+'/adapters/unreal/verify_scene.py'),'-nullrhi','-unattended','-nop4','-notraceserver','-NoZenAutoLaunch','-ddc=InstalledNoZenLocalFallback','-UDPMESSAGING_TRANSPORT_ENABLE=0',('-abslog='+$Output+'/unreal.log'))){$start.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($start)
 if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Own native extended verification timed out'}
 [void](Assert-SharedEditorFirewall -Executable $exe)
 if($process.ExitCode-ne0-or!(Test-Path (Join-Path $Output 'scene-execution.json'))){throw 'Native 250-frame execution failed'}
 & (Join-Path $PSScriptRoot 'compare-runtime-results.ps1') -ExpectedFile $UnityCapture -NativeFile (Join-Path $Output 'scene-execution.json') -PackageFile $package -Output $Output -Frames 250
 @{status='PASS';frames=250;pid=$process.Id;unityCapture=$UnityCapture;nativeCapture=(Join-Path $Output 'scene-execution.json');firewallPolicyChanged=$false;timestamp=(Get-Date).ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $Output 'audited-execution.json')
 Write-Output 'AUDITED_NATIVE_250_FRAME_COMPARISON_PASS'
}finally{$lease.Dispose()}
