param([Parameter(Mandatory=$true)][string]$Run,[string]$Output='artifacts/runtime-ui-api-tests/prototype-guard-verification.json')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);$Output=[IO.Path]::GetFullPath($Output);$root=[IO.Path]::GetFullPath('.');foreach($p in @($Run,$Output)){if(!$p.StartsWith($root+'\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}}
$m=Get-Content ($Run+'/Target/.EngineMigratorProject.json') -Raw|ConvertFrom-Json;$map=($m.units|Where-Object {$_.kind-eq'scene'-and$_.enabled}|Select-Object -First 1).map
$definition=($m.units|Where-Object {$_.kind-eq'prefab'-and$_.path-eq'Assets/RuntimeUIApi/UIActive.prefab'}|Select-Object -First 1).map;if(!$definition){throw 'UI definition map missing'}
@{maps=@($map,$definition);adapters=($root+'/adapters/unreal');output=$Output}|ConvertTo-Json|Set-Content ($Run+'/Target/EMUIPrototypeGuardJob.json')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @(($Run+'/Target/RuntimeWorld.uproject'),'-run=pythonscript',('-script='+$PSScriptRoot+'/verify-runtime-ui-prototype-guard.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$Output+'.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'UI prototype guard failed'};if((Get-Content $Output -Raw|ConvertFrom-Json).status-ne'PASS'){throw 'UI guard result missing'};Write-Output 'RUNTIME_UI_PROTOTYPE_GUARD_PASS'

