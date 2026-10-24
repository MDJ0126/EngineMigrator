param([Parameter(Mandatory=$true)][string]$Run,[string]$UnityProject,[string]$UnrealProject,[switch]$EndPlayOnly,[string]$BuiltPlugin,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe',[string]$UnrealEditor='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Run=[IO.Path]::GetFullPath($Run)
if(-not $Run.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Execution verification is limited to private artifacts copies.'}
$source=Join-Path $Run 'Source';$target=Join-Path $Run 'Target';$results=Join-Path $Run 'SceneExecution';New-Item -ItemType Directory -Path $results -Force|Out-Null
if($UnityProject){$source=[IO.Path]::GetFullPath($UnityProject);if(-not $source.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Unity verification project must be a private artifacts copy.'};$results=Join-Path $Run 'RestoredSceneExecution';New-Item -ItemType Directory -Path $results -Force|Out-Null}
if($UnrealProject){$target=[IO.Path]::GetFullPath($UnrealProject);if(-not $target.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Unreal verification project must be a private artifacts copy.'};$results=Join-Path $Run 'LatestNativeSceneExecution';New-Item -ItemType Directory -Path $results -Force|Out-Null}
if($EndPlayOnly){$results=Join-Path $results 'EndPlay';New-Item -ItemType Directory -Path $results -Force|Out-Null}
@'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[Serializable] public class SceneField {public string name,kind,value;}
[Serializable] public class SceneActor {public string id,className,owner;public Vector3 position,localPosition;public List<SceneField> state,destroyState;}
[Serializable] public class SceneExecution {public float[] deltas;public List<SceneActor> actors=new List<SceneActor>();public bool savedSceneExecuted=true,projectPortComplete=false;}
public class SceneFrameVerifier : MonoBehaviour {
 List<float> deltas=new List<float>();MonoBehaviour[] instances;
 public void Setup(){var package=JsonUtility.FromJson<EMPackage>(File.ReadAllText(Path.Combine(Application.dataPath,"../scene-source-package.json")));var names=package.nodes.SelectMany(n=>n.behaviours).Select(b=>b.className).ToArray();instances=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Where(c=>c&&names.Contains(c.GetType().Name)).ToArray();if(instances.Length!=names.Length||instances.Length==0)throw new Exception("Saved fixture instance count mismatch");}
 List<SceneField> Read(MonoBehaviour instance){return instance.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>f.FieldType==typeof(double)||f.FieldType==typeof(float)||f.FieldType==typeof(bool)||f.FieldType==typeof(int)||f.FieldType==typeof(Vector3)).Select(f=>new SceneField{name=f.Name,kind=f.FieldType==typeof(bool)?"bool":f.FieldType==typeof(Vector3)?"vector3":f.FieldType==typeof(float)?"single":"float",value=f.FieldType==typeof(Vector3)?JsonUtility.ToJson(f.GetValue(instance)):Convert.ToString(f.GetValue(instance),System.Globalization.CultureInfo.InvariantCulture)}).ToList();}
 void LateUpdate(){try{
  deltas.Add(Time.deltaTime);if(deltas.Count<4)return;
  var package=JsonUtility.FromJson<EMPackage>(File.ReadAllText(Path.Combine(Application.dataPath,"../scene-source-package.json")));var result=new SceneExecution{deltas=deltas.ToArray()};
  foreach(var instance in instances){var node=package.nodes.Single(n=>n.name==instance.gameObject.name);var actor=new SceneActor{id=node.behaviours.Single().id,className=instance.GetType().Name,owner=node.id,position=instance.transform.position,localPosition=instance.transform.localPosition,state=Read(instance)};DestroyImmediate(instance);actor.destroyState=Read(instance);result.actors.Add(actor);}
  File.WriteAllText(Path.Combine(Application.dataPath,"../scene-execution.json"),JsonUtility.ToJson(result,true));Debug.Log("ENGINE_MIGRATOR_SAVED_SCENE_UNITY_EXECUTED");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
public static class SavedSceneVerify {
 public static void Run(){EditorSceneManager.OpenScene(EditorBuildSettings.scenes.Single(s=>s.enabled).path);EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
 var deadline=EditorApplication.timeSinceStartup+90;EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Saved scene play timeout");EditorApplication.Exit(1);}};
 new GameObject("SceneFrameVerifier").AddComponent<SceneFrameVerifier>().Setup();EditorApplication.EnterPlaymode();}
}
'@ | Set-Content -LiteralPath "$source/Assets/Editor/SavedSceneVerify.cs"
Copy-Item -LiteralPath "$Run/Transfer/package.json" -Destination "$source/scene-source-package.json" -Force
function RunEditor([string]$Exe,[string[]]$Arguments){$start=[Diagnostics.ProcessStartInfo]::new($Exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;foreach($argument in $Arguments){$start.ArgumentList.Add($argument)};$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Editor execution failed: $Exe ($($process.ExitCode))"}}
RunEditor $UnityEditor @('-batchmode','-nographics','-projectPath',$source,'-executeMethod','SavedSceneVerify.Run','-logFile',"$results/unity.log")
$expected=Get-Content "$source/scene-execution.json" -Raw|ConvertFrom-Json
$plugin="$target/Plugins/EMBlueprintBridge";New-Item -ItemType Directory -Path $plugin -Force|Out-Null
$built=if($BuiltPlugin){$BuiltPlugin}else{"$workspace/artifacts/blueprint-plugin-5.8-compiled/HostProject/Plugins/EMBlueprintBridge"}
foreach($entry in @('EMBlueprintBridge.uplugin','Binaries')){Copy-Item -LiteralPath "$built/$entry" -Destination $plugin -Recurse -Force}
$project=(Get-ChildItem $target -Filter '*.uproject').FullName;$descriptor=Get-Content $project -Raw|ConvertFrom-Json
if(-not ($descriptor.Plugins|Where-Object Name -eq 'EMBlueprintBridge')){$descriptor.Plugins+=@{Name='EMBlueprintBridge';Enabled=$true}};$descriptor|ConvertTo-Json -Depth 10|Set-Content $project
@{deltas=$expected.deltas;endPlayOnly=[bool]$EndPlayOnly}|ConvertTo-Json|Set-Content "$results/input.json"
$nativeResult=Get-Content "$Run/Transfer/unreal-import-result.json" -Raw|ConvertFrom-Json
@{map=$nativeResult.savedMap;input="$results/input.json";output=$results}|ConvertTo-Json|Set-Content "$target/SceneVerify.json"
RunEditor $UnrealEditor @($project,'-run=pythonscript',"-script=$workspace/adapters/unreal/verify_scene.py",'-nullrhi','-unattended','-nop4',"-abslog=$results/unreal.log")
$actual=Get-Content "$results/scene-execution.json" -Raw|ConvertFrom-Json
if([bool]$actual.endPlayOnly-ne[bool]$EndPlayOnly){throw 'Requested native lifecycle termination path was not executed'}
if($actual.actors.Count -ne $expected.actors.Count){throw 'Scene behaviour instance count mismatch'}
foreach($actor in $expected.actors){
 $native=$actual.actors|Where-Object id -eq $actor.id;if(-not $native -or $native.owner -ne $actor.owner -or $native.className -ne $actor.className){throw "Scene owner/class mismatch: $($actor.id)"}
 foreach($phase in @('state','destroyState')){foreach($field in $actor.$phase){
  $value=$native.$phase.($field.name)
  if(-not $native.$phase.PSObject.Properties[$field.name]){throw "Native state field missing: $($field.name)"}
  if($field.kind -eq 'bool'){if([bool]::Parse($field.value) -ne $value){throw "Boolean state mismatch: $($field.name)"}}
  elseif($field.kind -eq 'vector3'){$vector=$field.value|ConvertFrom-Json;foreach($axis in @('x','y','z')){if([Math]::Abs([double]$vector.$axis-[double]$value.$axis)-gt 0.00001){throw "Vector state mismatch: $($field.name).$axis"}}}
  elseif(-not [double]::IsFinite([double]$field.value)-or -not [double]::IsFinite([double]$value)-or [Math]::Abs([double]$field.value-[double]$value)-gt $(if($field.kind-eq'single'){0.00001}else{0.00000001})){throw "Scene state mismatch: $($actor.id).$phase.$($field.name) Unity=$($field.value) Unreal=$value"}
 }}
 foreach($space in @('position','localPosition')){foreach($axis in @('x','y','z')){if([Math]::Abs([double]$actor.$space.$axis-[double]$native.$space.$axis)-gt 0.00001){throw "Scene Transform mismatch: $($actor.id).$space.$axis"}}}
}
@{status='PASS';savedSceneExecuted=$true;worldTickExecuted=$true;nativeEndPlayOnly=[bool]$EndPlayOnly;instances=$expected.actors.Count;ticks=$expected.deltas.Count;unity="$source/scene-execution.json";unreal="$results/scene-execution.json";projectPortComplete=$false}|ConvertTo-Json|Set-Content "$results/verification.json"
Write-Output "SAVED_SCENE_BEHAVIOUR_PASS: $results"
