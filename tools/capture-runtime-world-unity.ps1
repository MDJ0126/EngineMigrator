param([Parameter(Mandatory=$true)][string]$Project,[Parameter(Mandatory=$true)][string]$Output)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Project=[IO.Path]::GetFullPath($Project);$Output=[IO.Path]::GetFullPath($Output);$allowed=[IO.Path]::GetFullPath('artifacts')+'\'
foreach($path in @($Project,$Output)){if(!$path.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private artifacts only'}}
New-Item (Split-Path $Output) -ItemType Directory -Force|Out-Null
Copy-Item ($PSScriptRoot+'/runtime-probe/RuntimeWorldUnityProbe.cs') ($Project+'/Assets/Editor/RuntimeWorldUnityProbe.cs') -Force
$settings=$Project+'/ProjectSettings/EditorSettings.asset';$original=[IO.File]::ReadAllBytes($settings)
try{$env:EM_WORLD_OUTPUT=$Output;$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true
foreach($a in @('-batchmode','-nographics','-projectPath',$Project,'-executeMethod','EMRuntimeWorldProbe.Run','-logFile',($Output+'.log'))){$s.ArgumentList.Add($a)}
$p=[Diagnostics.Process]::Start($s);if(!$p.WaitForExit(240000)){$p.Kill();throw 'Own Unity runtime capture timed out'};if($p.ExitCode-ne 0){throw "Unity runtime capture failed: $Output.log"};Write-Output "UNITY_WORLD_CAPTURED: $Output"
}finally{[IO.File]::WriteAllBytes($settings,$original);Remove-Item Env:EM_WORLD_OUTPUT -ErrorAction SilentlyContinue}
