param([string]$Run='artifacts/runtime-creation-forward/PROJECT-aa436a07200d',[string]$Cli='artifacts/runtime-creation-cli-v4/EngineMigrator.Cli.dll')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);if(!$Run.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
Copy-Item ($PSScriptRoot+'/runtime-probe/RuntimeWorldProbe.cpp') ($Run+'/Target/Source/EMPortHost/EMPortHost.cpp') -Force
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project ($Run+'/Source')
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',($Run+'/Source'),'-executeMethod','EngineMigratorBridge.Run','-emMode','export-project','-emPackage',($Run+'/Transfer'),'-logFile',($Run+'/unity-creation-refresh.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Fixture export refresh failed'}
& ($PSScriptRoot+'/resume-runtime-prefab-fixture.ps1') -Run $Run -Cli $Cli
