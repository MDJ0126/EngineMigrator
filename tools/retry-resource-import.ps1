param([Parameter(Mandatory=$true)][string]$Run,[string]$UnrealEditor='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$Run=[IO.Path]::GetFullPath($Run)
if(!$Run.StartsWith(([IO.Path]::GetFullPath($workspace)+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Retry is limited to private artifacts copies'}
$target=Join-Path $Run 'Target';$package=Join-Path $Run 'Transfer';$project=(Get-ChildItem -LiteralPath $target -Filter '*.uproject').FullName
$id=(Get-Content (Join-Path (Split-Path $UnrealEditor) 'UnrealEditor.modules') -Raw|ConvertFrom-Json).BuildId
if($id-notmatch'^[A-Za-z0-9_-]+$'){throw 'Invalid build ID'}
$native=Join-Path $workspace "adapters/unreal/native/$id/EMBlueprintBridge"
if(!(Test-Path ($native+'/Binaries/Win64/UnrealEditor-EMBlueprintBridge.dll'))){throw 'Matching bridge required'}
Copy-Item ($native+'/Binaries/Win64/*') ($target+'/Plugins/EMBlueprintBridge/Binaries/Win64') -Force
$attempt='Retry_'+[guid]::NewGuid().ToString('N').Substring(0,8);$log=Join-Path $Run ($attempt+'.log')
@{package=$package;assetRoot='/Game/EngineMigrator_'+$attempt}|ConvertTo-Json|Set-Content ($target+'/EngineMigratorRun.json')
$start=[Diagnostics.ProcessStartInfo]::new($UnrealEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($a in @($project,'-run=pythonscript',('-script='+$workspace+'/adapters/unreal/resource_bridge.py'),'-nullrhi','-unattended','-nop4',('-abslog='+$log))){$start.ArgumentList.Add($a)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw "Retry failed ($($process.ExitCode)): $log"}
$result=Get-Content ($package+'/unreal-import-result.json') -Raw|ConvertFrom-Json
if(!$result.savedAndReloaded-or!$result.referencesVerified){throw 'Save/reference validation failed'}
Add-Content ($target+'/Config/DefaultEngine.ini') ("`n[/Script/EngineSettings.GameMapsSettings]`nEditorStartupMap="+$result.savedMap+"`nGameDefaultMap="+$result.savedMap)
@{RunDirectory=$Run;SourceCopy=(Join-Path $Run 'Source');TargetCopy=$target;VerificationFile=($package+'/unreal-import-result.json');ProjectPortComplete=$false}|ConvertTo-Json|Set-Content ($Run+'/resource-port-result.json')
Write-Output "RETRY_RESOURCE_IMPORT_PASS: $Run"
