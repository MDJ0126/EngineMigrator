param([string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot
$root=Join-Path $workspace ('artifacts/actor-unity-e2e/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'Assets/Editor'),(Join-Path $root 'ProjectSettings'),(Join-Path $root 'Packages') -Force | Out-Null
"m_EditorVersion: $((Get-Item -LiteralPath $UnityEditor).Directory.Parent.Name)" | Set-Content -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt')
'{"dependencies":{}}' | Set-Content -LiteralPath (Join-Path $root 'Packages/manifest.json')
foreach($variant in @('Original','Restored')){
 $folder=Join-Path $root ('Assets/'+$variant);New-Item -ItemType Directory -Path $folder | Out-Null
 foreach($file in Get-ChildItem -LiteralPath (Join-Path $workspace ('artifacts/actor-check/'+$variant)) -Filter '*.cs'){
  $source=[IO.File]::ReadAllText($file.FullName).Replace('using UnityEngine;','')
  ('using UnityEngine; namespace '+$variant+' {'+$source+'}') | Set-Content -LiteralPath (Join-Path $folder $file.Name)
 }
}
@'
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
[Serializable] public class ActorStateData {public double Health,Position,AwakeCount,StartCount,TickCount,DestroyCount,Bonus;public float[] deltas;public bool projectPortComplete=false;}
public class ActorFrameVerifier : MonoBehaviour {
 Original.PortDerived first;Restored.PortDerived last;List<float> deltas=new List<float>();
 public void Setup(){first=new GameObject("OriginalActor").AddComponent<Original.PortDerived>();last=new GameObject("RestoredActor").AddComponent<Restored.PortDerived>();}
 void LateUpdate(){
  try {
   deltas.Add(Time.deltaTime);if(first.TickCount<4.0)return;
   first.ApplyDamage(20.0);last.ApplyDamage(20.0);
   DestroyImmediate(first.gameObject);DestroyImmediate(last.gameObject);
   if(first.Health!=last.Health||first.Position!=last.Position||first.AwakeCount!=last.AwakeCount||first.StartCount!=last.StartCount||first.TickCount!=last.TickCount||first.DestroyCount!=last.DestroyCount||first.Bonus!=last.Bonus)throw new Exception("Original/restored MonoBehaviour state differs");
   if(first.Health!=89.0||first.AwakeCount!=1.0||first.StartCount!=1.0||first.TickCount!=4.0||first.DestroyCount!=3.0||first.Bonus!=4.0)throw new Exception("Unexpected lifecycle/parent dispatch state");
   var result=new ActorStateData{Health=first.Health,Position=first.Position,AwakeCount=first.AwakeCount,StartCount=first.StartCount,TickCount=first.TickCount,DestroyCount=first.DestroyCount,Bonus=first.Bonus,deltas=deltas.ToArray()};
   File.WriteAllText(Path.Combine(Application.dataPath,"../actor-execution.json"),JsonUtility.ToJson(result,true));Debug.Log("ENGINE_MIGRATOR_ACTOR_UNITY_PASS");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
public static class ActorUnityVerify {
 public static void Run(){
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
  var deadline=EditorApplication.timeSinceStartup+60;EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("Actor play test timed out");EditorApplication.Exit(1);}};
  EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode)new GameObject("FrameVerifier").AddComponent<ActorFrameVerifier>().Setup();};
  EditorApplication.EnterPlaymode();
 }
}
'@ | Set-Content -LiteralPath (Join-Path $root 'Assets/Editor/ActorUnityVerify.cs')
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-projectPath',$root,'-executeMethod','ActorUnityVerify.Run','-logFile',(Join-Path $root 'unity.log'))){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0-or -not(Test-Path -LiteralPath (Join-Path $root 'actor-execution.json'))){throw "Unity Actor lifecycle test failed: $root"}
Write-Output "ACTOR_UNITY_PASS: $root"
