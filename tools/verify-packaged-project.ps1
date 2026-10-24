param([string]$Project='artifacts/packaging-tests/UE58/Project',[string]$ExpectedRun='artifacts/extended-project-tests/RP-1b895bf1891b',[string]$Engine='C:/Program Files/Epic Games/UE_5.8/Engine',[switch]$SkipBuild)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$Project=[IO.Path]::GetFullPath($Project);$ExpectedRun=[IO.Path]::GetFullPath($ExpectedRun)
if(!$Project.StartsWith(($workspace+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Packaged verification requires a private project copy'}
$root=Split-Path $Project -Parent;$output=$root+'/PackagedRuntime';New-Item -ItemType Directory -Force $output|Out-Null
if(Test-Path ($output+'/verification.json')){Move-Item ($output+'/verification.json') ($output+'/previous-verification-'+[guid]::NewGuid().ToString('N')+'.json')}
if(!$SkipBuild){
 & dotnet ($workspace+'/artifacts/packaging-cli/EngineMigrator.Cli.dll') runtime-host $Project
 if($LASTEXITCODE-ne 0){throw 'Game host generation failed'}
 Copy-Item ($workspace+'/adapters/unreal/EMBlueprintBridge/Source') ($Project+'/Plugins/EMBlueprintBridge') -Recurse -Force
 $plugin=$Project+'/Plugins/EMRuntimeProbe';New-Item -ItemType Directory -Force $plugin|Out-Null;Copy-Item ($workspace+'/tools/runtime-probe/*') $plugin -Recurse -Force
 # Copy-Item preserves older source timestamps; a game binary built after the original
 # timestamp can otherwise remain stale even when the copied source bytes differ.
 Get-ChildItem ($plugin+'/Source') -Recurse -File|ForEach-Object {$_.LastWriteTimeUtc=[DateTime]::UtcNow}
 $descriptor=(Get-ChildItem $Project -Filter '*.uproject').FullName;$data=Get-Content $descriptor -Raw|ConvertFrom-Json
 if(!($data.Plugins|Where-Object Name -eq 'EMRuntimeProbe')){$data.Plugins+=@{Name='EMRuntimeProbe';Enabled=$true}}
 $data|ConvertTo-Json -Depth 10|Set-Content $descriptor
 try{
  $dotnet=(Get-ChildItem ($Engine+'/Binaries/ThirdParty/DotNet') -Recurse -Filter dotnet.exe|Select-Object -First 1).FullName
  & $dotnet ($Engine+'/Binaries/DotNET/UnrealBuildTool/UnrealBuildTool.dll') PortTargetEditor Win64 Development ('-Project='+$descriptor) -NoHotReload -MaxParallelActions=1 > ($root+'/probe-editor-build.log') 2>&1
  if($LASTEXITCODE-ne 0){throw 'Probe editor module build failed'}
 }finally{$data|ConvertTo-Json -Depth 10|Set-Content $descriptor}
 & ($Engine+'/Build/BatchFiles/RunUAT.bat') BuildCookRun ('-project='+$descriptor) -noP4 -platform=Win64 -clientconfig=Development -build -cook -stage -pak -archive ('-archivedirectory='+$root+'/PackagedProbe') -unattended -utf8output > ($root+'/probe-build-cook-run.log') 2>&1
 if($LASTEXITCODE-ne 0){throw 'Probe packaged build failed; see probe-build-cook-run.log'}
}
$exe=$root+'/PackagedProbe/Windows/PortTarget.exe';if(!(Test-Path $exe)){throw 'Packaged executable not found'}
$json=$output+'/scene-execution.json';if(Test-Path $json){Move-Item $json ($output+'/previous-'+[guid]::NewGuid().ToString('N')+'.json')}
$start=[Diagnostics.ProcessStartInfo]::new($exe);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
foreach($argument in @('-nullrhi','-unattended','-nosound','-NoSplash',('-EMProbeInput='+$ExpectedRun+'/ExtendedRuntime-250/unity-execution.json'),('-EMProbeOutput='+$json),('-abslog='+$output+'/game.log'))){$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start);$watch=[Diagnostics.Stopwatch]::StartNew()
while(!$process.WaitForExit(1000)){if($watch.Elapsed.TotalSeconds-gt 180){$process.Kill($true);throw 'Packaged game did not finish its probe within 180 seconds'}}
if($process.ExitCode-ne 0-or!(Test-Path $json)){throw 'Packaged game failed to write an actual runtime result'}
& ($workspace+'/tools/compare-runtime-results.ps1') -ExpectedFile ($ExpectedRun+'/ExtendedRuntime-250/unity-execution.json') -NativeFile $json -PackageFile ($ExpectedRun+'/Transfer/package.json') -Output $output -Frames 250
Write-Output "PACKAGED_GAME_RUNTIME_PASS: $output"
