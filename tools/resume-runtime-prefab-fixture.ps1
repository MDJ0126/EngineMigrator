param([string]$Run='artifacts/runtime-prefab-forward/PROJECT-ef68d60184bb',[string]$Cli='artifacts/runtime-prefab-cli-v4/EngineMigrator.Cli.dll')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=[IO.Path]::GetFullPath('.');$Run=[IO.Path]::GetFullPath($Run);if(!$Run.StartsWith($workspace+'\artifacts\',[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
function Editor([string]$exe,[string[]]$arguments){$s=[Diagnostics.ProcessStartInfo]::new($exe);$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in $arguments){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw "Fixture operation failed: $($p.ExitCode)"}}
& dotnet $Cli validate-project ($Run+'/Transfer');if($LASTEXITCODE-ne 0){throw 'Preflight failed'}
$scripts=@{};$manifest=Get-Content ($Run+'/Transfer/project-package.json') -Raw|ConvertFrom-Json;foreach($unit in $manifest.units){$data=Get-Content ($Run+'/Transfer/'+$unit.package+'/package.json') -Raw|ConvertFrom-Json;foreach($script in $data.scripts){$scripts[$script.name]=$script.source}}
foreach($unit in $manifest.units){& dotnet $Cli prepare-scene-settings ($Run+'/Transfer/'+$unit.package) ($Run+'/Target');if($LASTEXITCODE-ne 0){throw 'Scene settings preparation failed'}}
$source="using UnityEngine;`nusing UnityEngine.SceneManagement;`n"+(($scripts.Values|ForEach-Object {$_ -replace 'using UnityEngine(?:\.SceneManagement)?;',''}) -join "`n");$sourceFile=$Run+'/runtime-prefab-source.cs';Set-Content $sourceFile $source
$generated=$Run+'/generated-'+[guid]::NewGuid().ToString('N').Substring(0,8);& dotnet $Cli actors-to-cpp $sourceFile $generated;if($LASTEXITCODE-ne 0){throw 'Actor generation failed'};Copy-Item ($generated+'/*.h') ($Run+'/Target/Plugins/EMActorPort/Source/EMActorPort/Public') -Force
$ui=$Run+'/Target/Plugins/EMUIRuntime';if(!(Test-Path $ui)){Copy-Item ($workspace+'/artifacts/runtime-world-forward/PROJECT-1032d674297f/Target/Plugins/EMUIRuntime') $ui -Recurse}
$descriptor=$Run+'/Target/RuntimeWorld.uproject';$settings=Get-Content $descriptor -Raw|ConvertFrom-Json;if(!($settings.Plugins|Where-Object Name -eq EMUIRuntime)){$settings.Plugins+=@{Name='EMUIRuntime';Enabled=$true}};$settings|ConvertTo-Json -Depth 10|Set-Content $descriptor
$engine='C:/Program Files/Epic Games/UE_5.8/Engine';& dotnet $Cli install-bridge ($Run+'/Target') ($engine+'/Binaries/Win64/UnrealEditor-Cmd.exe') adapters;if($LASTEXITCODE-ne 0){throw 'Bridge installation failed'}
$dotnet=(Get-ChildItem ($engine+'/Binaries/ThirdParty/DotNet') -Filter dotnet.exe -Recurse|Select-Object -First 1).FullName
Editor $dotnet @(($engine+'/Binaries/DotNET/UnrealBuildTool/UnrealBuildTool.dll'),'RuntimeWorldEditor','Win64','Development',('-Project='+$descriptor),'-NoHotReload','-MaxParallelActions=1',('-log='+$Run+'/runtime-prefab-resume-build.log'))
@{mode='import';projectPackage=($Run+'/Transfer');assetRoot='/Game/RuntimePrefab_'+[guid]::NewGuid().ToString('N').Substring(0,8)}|ConvertTo-Json|Set-Content ($Run+'/Target/EngineMigratorRun.json')
Editor ($engine+'/Binaries/Win64/UnrealEditor-Cmd.exe') @($descriptor,'-run=pythonscript',('-script='+$workspace+'/adapters/unreal/project_bridge.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$Run+'/runtime-prefab-resume-import.log'))
Write-Output 'RUNTIME_PREFAB_RESUME_PASS'
