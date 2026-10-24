param([string]$Run='artifacts/runtime-placement-roundtrip/PROJECT-78356a204fc5',[string]$Forward='artifacts/runtime-placement-forward/PROJECT-4ea24955448e')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);$Forward=[IO.Path]::GetFullPath($Forward);$allowed=[IO.Path]::GetFullPath('artifacts')+'\';foreach($p in @($Run,$Forward)){if(!$p.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixtures only'}}
& ($PSScriptRoot+'/sync-test-adapters.ps1') -Project ($Run+'/Target')
function Unity([string]$mode,[string]$package,[string]$log){$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',($Run+'/Target'),'-executeMethod','EngineMigratorBridge.Run','-emMode',$mode,'-emPackage',$package,'-logFile',$log)){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw "Unity $mode failed: $log"}}
Unity 'import-project' ($Run+'/Transfer') ($Run+'/unity-project-retry.log')
Unity 'export-project' ($Run+'/ReexportedProject') ($Run+'/unity-project-reexport.log')
& 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($PSScriptRoot+'/compare-project-packages.py') ($Forward+'/Transfer') ($Run+'/ReexportedProject') ($Run+'/project-roundtrip-verification.json');if($LASTEXITCODE-ne 0){throw 'Project data differs'}
& dotnet artifacts/runtime-placement-cli-v6/EngineMigrator.Cli.dll report-project $Run Unreal;if($LASTEXITCODE-ne 0){throw 'Report failed'}
Write-Output 'PLACEMENT_PROJECT_ROUNDTRIP_PASS'
