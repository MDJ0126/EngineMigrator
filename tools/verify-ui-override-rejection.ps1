param([string]$Project='artifacts/ui-sprite-tests/RP-6c891568180e/Source')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
@'
using System;using System.IO;using System.Linq;using UnityEngine;using UnityEngine.UI;using UnityEditor;
public static partial class EngineMigratorBridge {
 public static void VerifyOverrideRejection(){var canvas=new GameObject("Test Canvas",typeof(RectTransform),typeof(Canvas));var go=new GameObject("Override Fixture",typeof(RectTransform),typeof(Image));go.transform.SetParent(canvas.transform,false);var image=go.GetComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UISpriteFixture/Left.asset");image.overrideSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UISpriteFixture/Right.asset");if(!image.sprite||image.sprite==image.overrideSprite)throw new Exception("Actual Unity override fixture is not distinct");var p=new EMPackage();ExportUI(go,new EMNode(),p);if(!p.blockers.Any(x=>x.Contains("overrideSprite")))throw new Exception("Distinct overrideSprite state was silently discarded");File.WriteAllText(Path.Combine(Application.dataPath,"../override-sprite-rejection.json"),"{\"status\":\"PASS\",\"actualUnityOverrideSprite\":true,\"unsupportedStateRejected\":true}");Debug.Log("UI_OVERRIDE_REJECTION_PASS");}
}
'@|Set-Content ($Project+'/Assets/Editor/EMOverrideRejection.cs')
$result=$Project+'/override-sprite-rejection.json';if(Test-Path $result){Move-Item $result ($Project+'/override-sprite-rejection-'+[guid]::NewGuid().ToString('N')+'.json')}
$start=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($argument in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EngineMigratorBridge.VerifyOverrideRejection','-logFile',($Project+'/override-rejection.log'))){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw 'Actual Unity override-state rejection test failed'}
if((Get-Content $result -Raw|ConvertFrom-Json).status-ne'PASS'){throw 'Unity override-state rejection evidence failed'}
Write-Output 'ACTUAL_UNITY_OVERRIDE_REJECTION_PASS'
