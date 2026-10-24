param([string]$Run='artifacts/runtime-animation-prefab-roundtrip-v6/PROJECT-e77467b81ddc',[string]$Forward='artifacts/runtime-animation-prefab-forward-v6/PROJECT-40ae2348bc19')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);$Forward=[IO.Path]::GetFullPath($Forward);$allowed=[IO.Path]::GetFullPath('artifacts')+'\';foreach($p in @($Run,$Forward)){if(!$p.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}}
& ($PSScriptRoot+'/capture-runtime-world-unity.ps1') -Project ($Run+'/Target') -Output 'artifacts/runtime-animation-prefab-tests/unity-restored.json'
$python='C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $python ($PSScriptRoot+'/compare-runtime-animation-prefab.py') artifacts/runtime-animation-prefab-tests/unity-original.json artifacts/runtime-animation-prefab-tests/unity-restored.json artifacts/runtime-animation-prefab-tests/restored-verification.json;if($LASTEXITCODE-ne 0){throw 'Restored runtime differs'}
$s=[Diagnostics.ProcessStartInfo]::new('C:/Program Files/Unity/Hub/Editor/6000.0.83f1/Editor/Unity.exe');$s.UseShellExecute=$false;$s.CreateNoWindow=$true;foreach($a in @('-batchmode','-nographics','-quit','-projectPath',($Run+'/Target'),'-executeMethod','EngineMigratorBridge.Run','-emMode','export-project','-emPackage',($Run+'/ReexportedProject'),'-logFile',($Run+'/unity-project-reexport.log'))){$s.ArgumentList.Add($a)};$p=[Diagnostics.Process]::Start($s);$p.WaitForExit();if($p.ExitCode-ne 0){throw 'Restored project reexport failed'}
& $python ($PSScriptRoot+'/compare-project-packages.py') ($Forward+'/Transfer') ($Run+'/ReexportedProject') ($Run+'/project-roundtrip-verification.json');if($LASTEXITCODE-ne 0){throw 'Project data differs'}
Write-Output 'RUNTIME_ANIMATION_PREFAB_PROJECT_ROUNDTRIP_PASS'















