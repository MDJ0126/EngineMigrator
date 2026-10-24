param([Parameter(Mandatory=$true)][string]$Run,[int]$Frames=4,[string]$UnityProject,[string]$Unit,[string]$UnityScene,[string]$OutputSuffix,[string]$UnrealProject,[string]$Map,[switch]$UnityOnly,[switch]$SkipUnity,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe',[string]$UnrealEditor='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$Run=[IO.Path]::GetFullPath($Run);$source=if($UnityProject){[IO.Path]::GetFullPath($UnityProject)}else{Join-Path $Run 'Source'};$target=Join-Path $Run 'Target';$output=Join-Path $Run ('ExtendedRuntime-'+$Frames+$(if($UnityProject){'-Restored'}else{''}))
if($UnrealProject){$target=[IO.Path]::GetFullPath($UnrealProject)}
$packageDir=if($Unit){Join-Path $Run ("Transfer/units/"+$Unit)}else{Join-Path $Run "Transfer"};if($Unit){if($Unit-notmatch"^[A-Za-z0-9_]+$"){throw "Invalid project unit"};$output+="-"+$Unit}
if($OutputSuffix){if($OutputSuffix-notmatch'^[A-Za-z0-9_-]+$'){throw 'Invalid runtime output suffix'};$output+='-'+$OutputSuffix}
if($Unit-and!$UnityScene){$catalog=if(Test-Path ($source+'/.EngineMigratorProject.json')){$source+'/.EngineMigratorProject.json'}else{$Run+'/Transfer/project-package.json'};$selected=(Get-Content $catalog -Raw|ConvertFrom-Json).units|Where-Object id -eq $Unit;if(!$selected-or$selected.kind-ne'scene'){throw 'Select an actual scene unit for runtime verification'};$UnityScene=if($selected.actualPath-and(Test-Path ($source+'/'+$selected.actualPath))){$selected.actualPath}else{$selected.path}}
foreach($path in @($Run,$source,$target)){if(!$path.StartsWith(([IO.Path]::GetFullPath($workspace)+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Runtime verification requires private copies'}}
New-Item -ItemType Directory -Path $output -Force|Out-Null
Copy-Item ($packageDir+'/package.json') ($source+'/extended-source-package.json') -Force
$settings=$source+'/ProjectSettings/EditorSettings.asset';$originalSettings=[IO.File]::ReadAllBytes($settings)
@'
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
[Serializable] public class EMRuntimeObject {public string id;public Vector3 position,scale;}
[Serializable] public class EMRuntimeFrame {public List<EMRuntimeObject> objects=new List<EMRuntimeObject>();}
[Serializable] public class EMRuntimeState {public string name,value;public bool boolean;}
[Serializable] public class EMRuntimeActor {public string id,className,owner;public List<EMRuntimeState> state,destroyState;}
[Serializable] public class EMRuntimeClick {public string button;public int count;}
[Serializable] public class EMRuntimeResult {public List<EMRuntimeClick> clicks=new List<EMRuntimeClick>();public List<float> deltas=new List<float>();public List<EMRuntimeFrame> frames=new List<EMRuntimeFrame>();public List<EMRuntimeActor> actors=new List<EMRuntimeActor>();}
public class EMExtendedFrameVerifier:MonoBehaviour {
 EMPackage package;Dictionary<string,GameObject> nodes;int count;EMRuntimeResult result=new EMRuntimeResult();
 public void Setup(){package=JsonUtility.FromJson<EMPackage>(File.ReadAllText(Path.Combine(Application.dataPath,"../extended-source-package.json")));nodes=EngineMigratorBridge.RuntimeNodeMap();count=int.Parse(Environment.GetEnvironmentVariable("EM_VERIFY_FRAMES")??"4");Time.captureDeltaTime=.02f;}
 List<EMRuntimeState> State(MonoBehaviour b)=>b.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance).Where(f=>f.FieldType==typeof(double)||f.FieldType==typeof(float)||f.FieldType==typeof(bool)).Select(f=>new EMRuntimeState{name=f.Name,value=Convert.ToString(f.GetValue(b),System.Globalization.CultureInfo.InvariantCulture),boolean=f.FieldType==typeof(bool)}).ToList();
 void Start(){Time.captureDeltaTime=.02f;}
 void LateUpdate(){try{result.deltas.Add(Time.deltaTime);Time.captureDeltaTime=.02f;var frame=new EMRuntimeFrame();foreach(var n in package.nodes){var t=nodes[n.id].transform;frame.objects.Add(new EMRuntimeObject{id=n.id,position=t.position,scale=t.lossyScale});}result.frames.Add(frame);if(result.frames.Count<count)return;
 foreach(var n in package.nodes.Where(x=>x.hasUI&&x.ui.visual=="button"&&x.ui.onClick.Count>0)){var button=nodes[n.id].GetComponent<UnityEngine.UI.Button>();button.onClick.Invoke();button.onClick.Invoke();result.clicks.Add(new EMRuntimeClick{button=n.id+"_Visual",count=2});}
 foreach(var n in package.nodes)foreach(var definition in n.behaviours){var instance=nodes[n.id].GetComponents<MonoBehaviour>().Single(b=>b.GetType().Name==definition.className);var data=new EMRuntimeActor{id=definition.id,owner=n.id,className=definition.className,state=State(instance)};DestroyImmediate(instance);data.destroyState=State(instance);result.actors.Add(data);}File.WriteAllText(Path.Combine(Application.dataPath,"../extended-runtime.json"),JsonUtility.ToJson(result,true));Debug.Log("ENGINE_MIGRATOR_EXTENDED_UNITY_EXECUTED");EditorApplication.Exit(0);
 }catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}}
}
public static class EMExtendedRuntime {
 public static void Run(){EditorSceneManager.OpenScene(Environment.GetEnvironmentVariable("EM_VERIFY_SCENE")??EditorBuildSettings.scenes.Single(s=>s.enabled).path);EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;new GameObject("Extended Runtime Verifier").AddComponent<EMExtendedFrameVerifier>().Setup();var deadline=EditorApplication.timeSinceStartup+120;EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup>deadline)EditorApplication.Exit(2);};EditorApplication.EnterPlaymode();}
}
public static partial class EngineMigratorBridge {public static Dictionary<string,GameObject> RuntimeNodeMap(){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();return SceneNodeIds(scene,scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray()).ToDictionary(e=>e.Value,e=>e.Key.gameObject);}}
'@|Set-Content ($source+'/Assets/Editor/EMExtendedRuntime.cs')
function Editor([string]$Exe,[string[]]$Arguments){$s=[Diagnostics.ProcessStartInfo]::new($Exe);$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in $Arguments){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw "Editor failed ($($p.ExitCode)): $output"}}
if(!$SkipUnity){try{$env:EM_VERIFY_FRAMES=[string]$Frames;if($UnityScene){$env:EM_VERIFY_SCENE=$UnityScene};Editor $UnityEditor @('-batchmode','-nographics','-projectPath',$source,'-executeMethod','EMExtendedRuntime.Run','-logFile',($output+'/unity.log'))}finally{[IO.File]::WriteAllBytes($settings,$originalSettings);Remove-Item Env:EM_VERIFY_FRAMES,Env:EM_VERIFY_SCENE -ErrorAction SilentlyContinue};Copy-Item ($source+'/extended-runtime.json') ($output+'/unity-execution.json') -Force}
if($UnityOnly){Write-Output "EXTENDED_UNITY_RUNTIME_RECORDED: $output";return}
$expected=Get-Content ($output+'/unity-execution.json') -Raw|ConvertFrom-Json
@{deltas=$expected.deltas;endPlayOnly=$true;clicks=@($expected.clicks)}|ConvertTo-Json|Set-Content ($output+'/input.json')
if(!$Map){$Map=(Get-Content ($packageDir+'/unreal-import-result.json') -Raw|ConvertFrom-Json).savedMap}
$actorNames=@{};$sourcePackage=Get-Content ($packageDir+'/package.json') -Raw|ConvertFrom-Json
if($sourcePackage.sourceEngine-eq'Unreal'){foreach($node in $sourcePackage.nodes){$actorNames[$node.name]=$node.id}}
@{map=$Map;input=($output+'/input.json');output=$output;actorNames=$actorNames}|ConvertTo-Json -Depth 8|Set-Content ($target+'/SceneVerify.json')
Editor $UnrealEditor @((Get-ChildItem $target -Filter '*.uproject').FullName,'-run=pythonscript',('-script='+$workspace+'/adapters/unreal/verify_scene.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$output+'/unreal.log'))
& ($workspace+'/tools/compare-runtime-results.ps1') -ExpectedFile ($output+'/unity-execution.json') -NativeFile ($output+'/scene-execution.json') -PackageFile ($packageDir+'/package.json') -Output $output -Frames $Frames
