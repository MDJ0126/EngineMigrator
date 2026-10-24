param(
 [Parameter(Mandatory=$true)][string]$ConvertedScript,
 [string]$UnityEditor='C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe'
)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$root=Join-Path $workspace ('artifacts/blueprint-unity-e2e/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $root 'Assets/Editor'),(Join-Path $root 'ProjectSettings'),(Join-Path $root 'Packages') -Force | Out-Null
$version=(Get-Item -LiteralPath $UnityEditor).Directory.Parent.Name
"m_EditorVersion: $version" | Set-Content -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt')
'{"dependencies":{}}' | Set-Content -LiteralPath (Join-Path $root 'Packages/manifest.json')
Copy-Item -LiteralPath $ConvertedScript -Destination (Join-Path $root 'Assets/CombatMath.cs')
@'
using System;
using System.IO;
using UnityEngine;
public static class BlueprintUnityVerify {
 public static void Run() {
  var values=new[]{CombatMath.Damage(10.0,3.0),CombatMath.Damage(2.0,5.0),CombatMath.Damage(0.0,0.0),CombatMath.Scale(-2.5,4.0)};
  var expected=new[]{7.0,0.0,0.0,-10.0};
  for(var i=0;i<values.Length;i++)if(values[i]!=expected[i])throw new Exception("Restored Blueprint function mismatch "+i);
  File.WriteAllText(Path.GetFullPath("unity-blueprint-verification.json"),"{\"status\":\"PASS\",\"cases\":4,\"projectPortComplete\":false}");
  Debug.Log("ENGINE_MIGRATOR_BLUEPRINT_UNITY_PASS");
 }
}
'@ | Set-Content -LiteralPath (Join-Path $root 'Assets/Editor/BlueprintUnityVerify.cs')
$start=[Diagnostics.ProcessStartInfo]::new($UnityEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($arg in @('-batchmode','-nographics','-quit','-projectPath',$root,'-executeMethod','BlueprintUnityVerify.Run','-logFile',(Join-Path $root 'unity.log'))){$start.ArgumentList.Add($arg)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0-or -not(Test-Path -LiteralPath (Join-Path $root 'unity-blueprint-verification.json'))){throw "Unity restored Blueprint test failed: $root"}
Write-Output "BLUEPRINT_UNITY_PASS: $root"
