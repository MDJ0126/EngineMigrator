param([Parameter(Mandatory=$true)][string]$Project,[string]$UnityEditor='C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent));$Project=[IO.Path]::GetFullPath($Project)
if(-not $Project.StartsWith(($workspace+'\artifacts\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture preparation is limited to private artifacts project copies.'}
Copy-Item -LiteralPath "$workspace/artifacts/readonly-cube.fbx" -Destination "$Project/Assets/SceneBehaviourFixture/ReadOnlyImported.fbx" -Force
Copy-Item "$workspace/adapters/unity/*.cs" "$Project/Assets/Editor" -Force
@'
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class ReadOnlyMeshFixture {
 public static void Run(){
  const string path="Assets/SceneBehaviourFixture/ReadOnlyImported.fbx";AssetDatabase.Refresh();var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.isReadable=true;importer.SaveAndReimport();var readable=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
  var expected=new EMMesh{name=readable.name,vertices=readable.vertices,normals=readable.normals,uv=readable.uv,triangles=readable.triangles};File.WriteAllText(Path.Combine(Application.dataPath,"../readonly-expected.json"),JsonUtility.ToJson(expected,true));
  importer.isReadable=false;importer.SaveAndReimport();var mesh=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();if(mesh.isReadable)throw new Exception("Fixture did not disable runtime mesh Read/Write");
  var scene=EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);var go=new GameObject("ReadOnly Imported Cube");go.transform.position=new Vector3(3,0,2);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/UrpResourceFixture/Material_png.mat");EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  EngineMigratorBridge.Run();
  Debug.Log("ENGINE_MIGRATOR_READONLY_FBX_EXPORTED");
 }
}
'@|Set-Content -LiteralPath "$Project/Assets/Editor/ReadOnlyMeshFixture.cs"
$output="$Project/ReadOnlyTransfer";$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$Project,'-executeMethod','ReadOnlyMeshFixture.Run','-emMode','export','-emPackage',$output,'-logFile',"$Project/readonly-fixture.log")){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode -ne 0){throw "Read-only mesh export failed: $Project/readonly-fixture.log"}
$expected=Get-Content "$Project/readonly-expected.json" -Raw|ConvertFrom-Json;$package=Get-Content "$output/package.json" -Raw|ConvertFrom-Json;$actual=$package.meshes|Where-Object name -eq $expected.name
if(-not $actual){throw 'Imported FBX mesh missing from exported package'}
foreach($field in @('vertices','normals','uv','triangles')){if(($expected.$field|ConvertTo-Json -Depth 10 -Compress)-ne($actual.$field|ConvertTo-Json -Depth 10 -Compress)){throw "Read-only mesh data differs: $field"}}
@{status='PASS';actualFbxImport=$true;runtimeReadWrite=$false;importerModifiedByExporter=$false;vertices=$actual.vertices.Count;triangles=$actual.triangles.Count/3;projectPortComplete=$false}|ConvertTo-Json|Set-Content "$Project/readonly-verification.json"
Write-Output "READONLY_FBX_PASS: $Project/readonly-verification.json"
