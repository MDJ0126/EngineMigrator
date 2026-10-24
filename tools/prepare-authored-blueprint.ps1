param([string]$UnrealEditor='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$root=Join-Path $workspace 'artifacts/authored-blueprint-test';$source=Join-Path $root 'Source'
if(Test-Path $source){throw 'Authored Blueprint fixture already exists'}
New-Item -ItemType Directory -Path $source -Force|Out-Null
$base=Join-Path $workspace 'artifacts/extended-project-tests/RP-67692c17d09a/Target'
foreach($entry in @('Content','Config','Plugins','PortTarget.uproject')){Copy-Item -LiteralPath ($base+'/'+$entry) -Destination $source -Recurse}
$built=Join-Path $workspace 'artifacts/blueprint-build/d597a8a97f7944d791fac7db4948c851/HostProject/Plugins/EMBlueprintBridge/Binaries/Win64'
Copy-Item ($built+'/UnrealEditor-EMBlueprintBridge.dll') ($source+'/Plugins/EMBlueprintBridge/Binaries/Win64') -Force
$graph=Get-Content (Join-Path $workspace 'artifacts/extended-project-tests/actual-actor-graph.json') -Raw
$graph=$graph.Replace('GraphBase','BP_AuthoredBase').Replace('GraphDerived','BP_AuthoredChild');$graph|Set-Content ($root+'/input.json')
@'
import json,os,unreal
project=unreal.Paths.project_dir();root=os.path.abspath(os.path.join(project,'..'))
with open(os.path.join(root,'input.json'),encoding='utf-8-sig') as f:graph=json.load(f)
graph['assetRoot']='/Game/NativeAuthoredBlueprint'
value=unreal.EMBlueprintLibrary.actor_graph(json.dumps(graph))
if value.startswith('ERROR:'):raise RuntimeError(value)
data=json.loads(value);leaf=data['classes'][-1]['asset']
world=unreal.get_editor_subsystem(unreal.LevelEditorSubsystem).new_level('/Game/NativeAuthoredBlueprint/SourceMap')
actor=unreal.get_editor_subsystem(unreal.EditorActorSubsystem).spawn_actor_from_class(unreal.load_class(None,leaf+'_C'),unreal.Vector(200,300,400))
actor.set_actor_label('Native Blueprint Counter');actor.tags=[];actor.set_editor_property('Value',7.0);actor.set_editor_property('Extra',9.0);actor.set_editor_property('Enabled',False)
if not unreal.EditorLoadingAndSavingUtils.save_map(unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world(),'/Game/NativeAuthoredBlueprint/SourceMap'):raise RuntimeError('Map save failed')
if unreal.EditorAssetLibrary.get_metadata_tag(actor,'EngineMigratorBehaviour'):raise RuntimeError('Unexpected migration ownership metadata')
extracted=unreal.EMBlueprintLibrary.actor_graph(json.dumps({'assets':[leaf]}))
if extracted.startswith('ERROR:'):raise RuntimeError(extracted)
if len(json.loads(extracted)['classes'])!=2:raise RuntimeError('Native Blueprint parent collection failed')
with open(os.path.join(root,'fixture-verification.json'),'w',encoding='utf-8') as f:json.dump({'status':'PASS','actualActorBlueprintAssets':2,'existingLeafAssetExtractedWithParent':True,'migrationOwnershipMetadata':False,'map':'/Game/NativeAuthoredBlueprint/SourceMap'},f,indent=2)
'@|Set-Content ($root+'/prepare.py')
$start=[Diagnostics.ProcessStartInfo]::new($UnrealEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($a in @(($source+'/PortTarget.uproject'),'-run=pythonscript',('-script='+$root+'/prepare.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$root+'/prepare.log'))){$start.ArgumentList.Add($a)}
$p=[Diagnostics.Process]::Start($start);$p.WaitForExit();if($p.ExitCode-ne 0){throw "Native Blueprint preparation failed: $root/prepare.log"}
Add-Content ($source+'/Config/DefaultEngine.ini') "`n[/Script/EngineSettings.GameMapsSettings]`nEditorStartupMap=/Game/NativeAuthoredBlueprint/SourceMap`nGameDefaultMap=/Game/NativeAuthoredBlueprint/SourceMap"
Write-Output "AUTHORED_BLUEPRINT_PREPARED: $source"
