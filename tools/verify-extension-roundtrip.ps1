param([Parameter(Mandatory=$true)][string]$ForwardRun,[Parameter(Mandatory=$true)][string]$ReverseRun,[int]$Frames=250,[string]$UnrealEditor='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot
$ForwardRun=[IO.Path]::GetFullPath($ForwardRun);$ReverseRun=[IO.Path]::GetFullPath($ReverseRun)
foreach($path in @($ForwardRun,$ReverseRun)){if(!$path.StartsWith(([IO.Path]::GetFullPath($workspace)+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Verification requires private artifacts copies'}}
$project=$ReverseRun+'/Target';$package=$ReverseRun+'/Transfer';$restored=$ReverseRun+'/RestoredPackage'
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project $project
function Unity([string]$mode,[string]$data,[string]$log){
 $start=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 foreach($argument in @('-batchmode','-nographics','-quit','-projectPath',$project,'-executeMethod','EngineMigratorBridge.Run','-emMode',$mode,'-emPackage',$data,'-logFile',$log)){$start.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw "Unity $mode failed: $log"}
}
Unity 'import' $package ($ReverseRun+'/extended-reimport.log')
Unity 'export' $restored ($ReverseRun+'/restored-export.log')
& 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($workspace+'/tools/compare-scene-packages.py') ($ForwardRun+'/Transfer/package.json') ($restored+'/package.json') ($ReverseRun+'/roundtrip-data-verification.json')
if($LASTEXITCODE-ne 0){throw 'Roundtrip actual scene data differs'}
& ($workspace+'/tools/verify-extended-runtime.ps1') -Run $ForwardRun -UnityProject $project -UnrealProject ($ForwardRun+'/Target') -Frames $Frames -UnrealEditor $UnrealEditor
Write-Output "EXTENSION_ROUNDTRIP_PASS: $ReverseRun"
