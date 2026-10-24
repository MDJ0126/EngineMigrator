param([string]$Run='artifacts/runtime-world-forward/PROJECT-1032d674297f',[string]$Cli='artifacts/runtime-world-cli-v4/EngineMigrator.Cli.dll')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath('.');$Run=[IO.Path]::GetFullPath($Run);if(!$Run.StartsWith($workspace+'\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
function Editor([string]$exe,[string[]]$argsList){$s=[Diagnostics.ProcessStartInfo]::new($exe);$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in $argsList){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw "Runtime fixture operation failed: $($p.ExitCode)"}}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project ($Run+'/Source')
Editor 'C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe' @('-batchmode','-nographics','-quit','-projectPath',($Run+'/Source'),'-executeMethod','EngineMigratorBridge.Run','-emMode','export-project','-emPackage',($Run+'/Transfer'),'-logFile',($Run+'/runtime-world-refresh-export.log'))
& dotnet $Cli validate-project ($Run+'/Transfer');if($LASTEXITCODE-ne 0){throw 'Preflight failed'}
$scripts=@{};$manifest=Get-Content ($Run+'/Transfer/project-package.json') -Raw|ConvertFrom-Json;foreach($unit in $manifest.units){$data=Get-Content ($Run+'/Transfer/'+$unit.package+'/package.json') -Raw|ConvertFrom-Json;foreach($script in $data.scripts){$scripts[$script.name]=$script.source}}
$source="using UnityEngine;`nusing UnityEngine.SceneManagement;`n"+ (($scripts.Values|ForEach-Object {$_ -replace 'using UnityEngine(?:\.SceneManagement)?;',''}) -join "`n");$sourceFile=$Run+'/runtime-world-source.cs';Set-Content $sourceFile $source
$generated=$Run+'/generated-'+[guid]::NewGuid().ToString('N').Substring(0,8);& dotnet $Cli actors-to-cpp $sourceFile $generated;if($LASTEXITCODE-ne 0){throw 'Actor generation failed'}
Copy-Item ($generated+'/*.h') ($Run+'/Target/Plugins/EMActorPort/Source/EMActorPort/Public') -Force
Copy-Item ($PSScriptRoot+'/runtime-probe/RuntimeWorldProbe.cpp') ($Run+'/Target/Source/EMPortHost/EMPortHost.cpp') -Force
$engine='C:/Program Files/Epic Games/UE_5.8/Engine';$dotnet=(Get-ChildItem ($engine+'/Binaries/ThirdParty/DotNet') -Filter dotnet.exe -Recurse|Select-Object -First 1).FullName
Editor $dotnet @(($engine+'/Binaries/DotNET/UnrealBuildTool/UnrealBuildTool.dll'),'RuntimeWorldEditor','Win64','Development',('-Project='+$Run+'/Target/RuntimeWorld.uproject'),'-NoHotReload','-MaxParallelActions=1',('-log='+$Run+'/runtime-world-refresh-build.log'))
@{mode='import';projectPackage=($Run+'/Transfer');assetRoot='/Game/RuntimeWorld_'+[guid]::NewGuid().ToString('N').Substring(0,8)}|ConvertTo-Json|Set-Content ($Run+'/Target/EngineMigratorRun.json')
Editor ($engine+'/Binaries/Win64/UnrealEditor-Cmd.exe') @(($Run+'/Target/RuntimeWorld.uproject'),'-run=pythonscript',('-script='+$workspace+'/adapters/unreal/project_bridge.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$Run+'/runtime-world-refresh-import.log'))
Write-Output 'RUNTIME_WORLD_REFRESH_PASS'
