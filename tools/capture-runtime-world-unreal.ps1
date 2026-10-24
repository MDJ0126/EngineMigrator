param([string]$Run='artifacts/runtime-world-forward/PROJECT-1032d674297f',[string]$Output='artifacts/runtime-world-tests/unreal-execution.json',[string]$Map)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);$Output=[IO.Path]::GetFullPath($Output);$allowed=[IO.Path]::GetFullPath('artifacts')+'\'
foreach($path in @($Run,$Output)){if(!$path.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}}
$manifest=Get-Content ($Run+'/Target/.EngineMigratorProject.json') -Raw|ConvertFrom-Json
if($Map){if(!($manifest.units|Where-Object {$_.kind-eq'scene'-and$_.map-eq$Map})){throw 'Select a tracked scene map'}}else{$Map=($manifest.units|Where-Object {$_.kind-eq'scene'-and$_.enabled}|Select-Object -First 1).map}
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true
foreach($a in @(($Run+'/Target/RuntimeWorld.uproject'),$map,'-game','-nullrhi','-RenderOffscreen','-nosound','-unattended','-nop4','-UseFixedTimeStep','-FPS=50',('-EMRuntimeProbe='+$Output),('-abslog='+$Output+'.log'))){$s.ArgumentList.Add($a)}
$p=[Diagnostics.Process]::Start($s);if(!$p.WaitForExit(180000)){$p.Kill();throw 'Own Unreal runtime capture timed out'};if($p.ExitCode-ne 0){throw "Unreal runtime capture failed ($($p.ExitCode)): $Output.log"};if(!(Test-Path $Output)){throw 'Unreal runtime probe produced no capture'};Write-Output "UNREAL_WORLD_CAPTURED: $Output"

