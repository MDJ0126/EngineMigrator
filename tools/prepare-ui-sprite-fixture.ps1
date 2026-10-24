param([string]$Project='artifacts/extended-project-tests/UnitySource')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$Project=[IO.Path]::GetFullPath($Project)
if(!$Project.StartsWith(($workspace+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'UI fixture requires a private copy'}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $Project
@'
using System;using System.IO;using UnityEngine;using UnityEngine.UI;using UnityEditor;using UnityEditor.SceneManagement;
public static class EMPrepareUISprites {
 static GameObject Rect(string name,Transform parent,Vector2 position){var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(128,128);return go;}
 public static void Run(){
  var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);if(GameObject.Find("Sprite Left"))throw new Exception("Sprite fixture already exists");
  Directory.CreateDirectory("Assets/UISpriteFixture");var tex=new Texture2D(8,4,TextureFormat.RGBA32,false);var colors=new Color[32];for(int y=0;y<4;y++)for(int x=0;x<8;x++)colors[y*8+x]=x<4?(y<2?(x<2?Color.red:Color.green):(x<2?Color.blue:Color.yellow)):((x+y)%2==0?Color.magenta:Color.cyan);tex.SetPixels(colors);tex.Apply();File.WriteAllBytes("Assets/UISpriteFixture/Atlas.png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.Refresh();
  var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/UISpriteFixture/Atlas.png");importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();tex=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UISpriteFixture/Atlas.png");
  var left=Sprite.Create(tex,new Rect(0,0,4,4),new Vector2(.25f,.75f),100,0,SpriteMeshType.FullRect);left.name="Atlas Left";AssetDatabase.CreateAsset(left,"Assets/UISpriteFixture/Left.asset");var right=Sprite.Create(tex,new Rect(4,0,4,4),new Vector2(.5f,.5f),80,0,SpriteMeshType.FullRect);right.name="Atlas Right";AssetDatabase.CreateAsset(right,"Assets/UISpriteFixture/Right.asset");
  var canvas=GameObject.Find("Port UI");Rect("Sprite Left",canvas.transform,new Vector2(-180,-100)).AddComponent<Image>().sprite=left;Rect("Sprite Right",canvas.transform,new Vector2(0,-100)).AddComponent<Image>().sprite=right;
  var raw=Rect("Atlas RawImage",canvas.transform,new Vector2(180,-100)).AddComponent<RawImage>();raw.texture=tex;raw.uvRect=new Rect(.5f,0,.5f,1);
  var button=GameObject.Find("Action Button");button.GetComponent<Image>().sprite=left;var label=Rect("Button Child Text",button.transform,Vector2.zero).AddComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text="PORT";label.fontSize=20;label.alignment=TextAnchor.MiddleCenter;label.supportRichText=false;label.raycastTarget=true;
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Debug.Log("EM_UI_SPRITE_FIXTURE_READY");
 }
}
'@|Set-Content ($Project+'/Assets/Editor/EMPrepareUISprites.cs')
$start=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($argument in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','EMPrepareUISprites.Run','-logFile',($Project+'/ui-sprite-fixture.log'))){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw 'UI sprite fixture preparation failed'}
Write-Output "UI_SPRITE_FIXTURE_READY: $Project"
