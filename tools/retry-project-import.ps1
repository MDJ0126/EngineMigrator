param([Parameter(Mandatory=$true)][string]$Run,[string]$Cli='artifacts/project-cli-v4/EngineMigrator.Cli.dll')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$Run=[IO.Path]::GetFullPath($Run)
if(!$Run.StartsWith(($workspace+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Retry is restricted to private fixtures'}
$source=Join-Path $Run 'Source';$target=Join-Path $Run 'Target';$transfer=Join-Path $Run 'Transfer';& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $source
function Editor([string]$exe,[string[]]$arguments){$s=[Diagnostics.ProcessStartInfo]::new($exe);$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in $arguments){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw "Engine retry failed ($($p.ExitCode)): $Run"}}
Editor 'C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe' @('-batchmode','-nographics','-quit','-projectPath',$source,'-executeMethod','EngineMigratorBridge.Run','-emMode','export-project','-emPackage',$transfer,'-logFile',($Run+'/unity-project-retry.log'))
& dotnet $Cli validate-project $transfer;if($LASTEXITCODE-ne 0){throw 'Retry preflight failed'}
& dotnet $Cli install-bridge $target 'C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe' adapters;if($LASTEXITCODE-ne 0){throw 'Retry native installation failed'}
@{mode='import';projectPackage=$transfer;assetRoot='/Game/EM_Project_Retry_'+[guid]::NewGuid().ToString('N').Substring(0,8)}|ConvertTo-Json|Set-Content ($target+'/EngineMigratorRun.json')
Editor 'C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe' @((Get-ChildItem $target -Filter '*.uproject').FullName,'-run=pythonscript',('-script='+$workspace+'/adapters/unreal/project_bridge.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$Run+'/unreal-project-retry.log'))
$result=Get-Content ($transfer+'/unreal-import-result.json') -Raw|ConvertFrom-Json;if(!$result.savedAndReloaded-or!$result.referencesVerified){throw 'Retry verification failed'};Add-Content ($target+'/Config/DefaultEngine.ini') ("`n[/Script/EngineSettings.GameMapsSettings]`nEditorStartupMap="+$result.savedMap+"`nGameDefaultMap="+$result.savedMap);Write-Output 'PROJECT_RETRY_IMPORT_PASS'
