param([string]$ForwardRun='artifacts/runtime-prefab-forward/PROJECT-ef68d60184bb',[string]$Output='artifacts/runtime-prefab-packaging',[switch]$SkipCopy,[switch]$DisableUba,[string]$Comparer='compare-runtime-prefab.py',[string]$Expected='artifacts/runtime-prefab-tests/unity-original.json')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$ForwardRun=[IO.Path]::GetFullPath($ForwardRun);$Output=[IO.Path]::GetFullPath($Output);$allowed=[IO.Path]::GetFullPath('artifacts')+'\';foreach($p in @($ForwardRun,$Output)){if(!$p.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private artifacts only'}}
$source=$ForwardRun+'/Target';$project=$Output+'/Project';if(!$SkipCopy){if(Test-Path $project){throw 'Use a new packaging output directory'};New-Item $Output -ItemType Directory -Force|Out-Null;& 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($PSScriptRoot+'/copy-private-unreal.py') $source $project;if($LASTEXITCODE-ne 0){throw 'Private packaging copy failed'}}
$manifest=Get-Content ($project+'/.EngineMigratorProject.json') -Raw|ConvertFrom-Json;$maps=@($manifest.units|Where-Object {$_.kind-eq'scene'-and$_.enabled}|ForEach-Object {$_.map});if($maps.Count-ne 3){throw 'Expected three actual runtime test maps'};foreach($map in $maps){if(!$map.StartsWith('/Game/')-or!(Test-Path ($project+'/Content/'+$map.Substring(6)+'.umap'))){throw 'Packaging snapshot does not contain the requested actual map'}}
Add-Content ($project+'/Config/DefaultEngine.ini') ("`n[/Script/EngineSettings.GameMapsSettings]`nEditorStartupMap="+$maps[0]+"`nGameDefaultMap="+$maps[0])
Add-Content ($project+'/Config/DefaultGame.ini') ("`n[/Script/UnrealEd.ProjectPackagingSettings]`n"+(($maps|ForEach-Object {'+MapsToCook=(FilePath="'+$_+'")'}) -join "`n"))
$descriptor=$project+'/RuntimeWorld.uproject'
& 'C:/Program Files/Epic Games/UE_5.8/Engine/Build/BatchFiles/RunUAT.bat' BuildCookRun ('-project='+$descriptor) -noP4 -platform=Win64 -clientconfig=Development -build -cook -stage -pak -archive ('-archivedirectory='+$Output+'/Archive') ('-map='+($maps -join '+')) -unattended -utf8output ('-ubtargs=-MaxParallelActions=1'+$(if($DisableUba){' -NoUBA'}else{''})) *> ($Output+'/build-cook-run.log')
if($LASTEXITCODE-ne 0){throw "Runtime prefab build/cook failed: $Output/build-cook-run.log"}
& (Join-Path $PSScriptRoot 'execute-packaged-runtime.ps1') -Packaging $Output -Comparer $Comparer -Expected $Expected
Write-Output 'RUNTIME_PREFAB_PACKAGED_EXECUTION_PASS'
