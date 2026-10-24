param(
 [Parameter(Mandatory=$true)][string]$UnityResult,
 [string]$UnrealEditor='C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe',
 [string]$BuiltPlugin=(Join-Path $PSScriptRoot '../artifacts/blueprint-plugin-5.8-compiled/HostProject/Plugins/EMBlueprintBridge'),
 [string]$ActorPlugin,
 [string]$ClassPath='/Script/EMBlueprintBridge.PortDerived'
)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot
$root=Join-Path $workspace ('artifacts/actor-unreal-e2e/'+[guid]::NewGuid().ToString('N'));$project=Join-Path $root 'Project';$plugin=Join-Path $project 'Plugins/EMBlueprintBridge';$results=Join-Path $root 'Results'
New-Item -ItemType Directory -Path $plugin,$results -Force | Out-Null
foreach($entry in @('EMBlueprintBridge.uplugin','Binaries')){Copy-Item -LiteralPath (Join-Path $BuiltPlugin $entry) -Destination $plugin -Recurse}
'{"FileVersion":3,"EngineAssociation":"5.8","Plugins":[{"Name":"PythonScriptPlugin","Enabled":true},{"Name":"EMBlueprintBridge","Enabled":true}]}' | Set-Content -LiteralPath (Join-Path $project 'ActorPort.uproject')
if($ActorPlugin){$extra=Join-Path $project 'Plugins/EMActorPort';New-Item -ItemType Directory -Path $extra | Out-Null;foreach($entry in @('EMActorPort.uplugin','Binaries')){Copy-Item -LiteralPath (Join-Path $ActorPlugin $entry) -Destination $extra -Recurse};$descriptor=Get-Content -LiteralPath (Join-Path $project 'ActorPort.uproject') -Raw | ConvertFrom-Json;$descriptor.Plugins+=@{Name='EMActorPort';Enabled=$true};$descriptor | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $project 'ActorPort.uproject')}
$expected=Get-Content -LiteralPath $UnityResult -Raw | ConvertFrom-Json
$inputFile=Join-Path $root 'input.json';@{classPath=$ClassPath;deltas=$expected.deltas} | ConvertTo-Json | Set-Content -LiteralPath $inputFile
@{input=$inputFile;output=$results} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'ActorRun.json')
$start=[Diagnostics.ProcessStartInfo]::new($UnrealEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @((Join-Path $project 'ActorPort.uproject'),'-run=pythonscript',('-script='+(Join-Path $workspace 'adapters/unreal/verify_actors.py')),'-nullrhi','-unattended','-nop4',('-abslog='+(Join-Path $root 'unreal.log')))){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw "Native Actor lifecycle failed: $root"}
$actual=Get-Content -LiteralPath (Join-Path $results 'actor-execution.json') -Raw | ConvertFrom-Json
foreach($field in @('Health','Position','AwakeCount','StartCount','TickCount','DestroyCount','Bonus')){if([Math]::Abs($expected.$field-$actual.$field)-gt 0.00000001){throw "Actor state mismatch $field : Unity=$($expected.$field) Unreal=$($actual.$field)"}}
@{status='PASS';unityResult=(Resolve-Path -LiteralPath $UnityResult).Path;nativeResult=(Join-Path $results 'actor-execution.json');fields=7;ticks=4;projectPortComplete=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'verification.json')
Write-Output "ACTOR_UNREAL_PASS: $root"
