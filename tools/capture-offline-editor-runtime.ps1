param([Parameter(Mandatory=$true)][string]$Run,[Parameter(Mandatory=$true)][string]$Output,[string]$Scene='UIApi',[switch]$Plan)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
. (Join-Path $PSScriptRoot 'Assert-SharedEditorFirewall.ps1')
$Run=Assert-OfflineRuntimePath $Run;$Output=Assert-OfflineRuntimePath $Output
if(Test-Path -LiteralPath $Output){throw 'A fresh capture output is required'}
$descriptor=Join-Path $Run 'Target/RuntimeWorld.uproject'
if(!(Test-Path -LiteralPath $descriptor)){throw 'Existing private runtime project required'}
$manifest=Get-Content (Join-Path $Run 'Target/.EngineMigratorProject.json') -Raw|ConvertFrom-Json
$selected=@($manifest.units|Where-Object {$_.kind-eq'scene'-and$_.name-eq$Scene})
if($selected.Count-ne 1-or$selected[0].map-notmatch'^/Game/[A-Za-z0-9_/]+$'){throw 'Select one tracked scene'}
$exe='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe'
$arguments=@($descriptor,$selected[0].map,'-game','-nullrhi','-RenderOffscreen','-nosound','-unattended','-nop4','-notraceserver','-NoZenAutoLaunch','-ddc=InstalledNoZenLocalFallback','-UDPMESSAGING_TRANSPORT_ENABLE=0','-UseFixedTimeStep','-FPS=50',('-EMRuntimeProbe='+$Output),('-abslog='+$Output+'.log'))
$preflight=Assert-SharedEditorFirewall -Executable $exe
if($Plan){[pscustomobject]@{executable=$exe;arguments=$arguments;preflight=$preflight;engineStarted=$false;policyChanged=$false}|ConvertTo-Json -Depth 7;return}
# User delegated continuation after the shared-engine approval question on 2026-10-23.
# Only this audited, existing private-project runtime launch is allowed; no builds.
$lease=[IO.File]::Open((Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try{
 $preflight=Assert-SharedEditorFirewall -Executable $exe
 New-Item -ItemType Directory -Path (Split-Path $Output -Parent) -Force|Out-Null
 $preflight|ConvertTo-Json -Depth 6|Set-Content ($Output+'.firewall-before.json')
 $start=[Diagnostics.ProcessStartInfo]::new($exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($start)
 if(!$process.WaitForExit(180000)){$process.Kill($true);$process.WaitForExit();throw 'Own audited editor runtime timed out'}
 $after=Assert-SharedEditorFirewall -Executable $exe;$after|ConvertTo-Json -Depth 6|Set-Content ($Output+'.firewall-after.json')
 if(($after.profiles-join',')-ne($preflight.profiles-join',')){throw 'Network profile changed during capture'}
 if($process.ExitCode-ne 0-or!(Test-Path -LiteralPath $Output)){throw ('Editor runtime capture failed: '+$process.ExitCode)}
 @{status='CAPTURED';exitCode=$process.ExitCode;pid=$process.Id;capture=$Output;scene=$Scene;executable=$exe;arguments=$arguments;firewallPolicyChanged=$false;timestamp=(Get-Date).ToString('o')}|ConvertTo-Json -Depth 4|Set-Content ($Output+'.execution.json')
 Write-Output 'AUDITED_EDITOR_RUNTIME_CAPTURED'
}finally{$lease.Dispose()}
