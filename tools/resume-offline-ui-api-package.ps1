# Narrow continuation of the interrupted private UI API package. No arbitrary project arguments.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
. (Join-Path $PSScriptRoot 'Assert-SharedEditorFirewall.ps1')
$root=Split-Path $PSScriptRoot -Parent
$output=Assert-OfflineRuntimePath (Join-Path $root 'artifacts/runtime-ui-api-packaging-v2')
$project=Join-Path $output 'Project';$descriptor=Join-Path $project 'RuntimeWorld.uproject'
$editor='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe'
$preflight=Assert-SharedEditorFirewall -Executable $editor
if((Get-PSDrive C).Free-lt1000000000){throw 'Insufficient disk headroom before private packaging'}
$manifest=Get-Content (Join-Path $project '.EngineMigratorProject.json') -Raw|ConvertFrom-Json
$maps=@($manifest.units|Where-Object {$_.kind-eq'scene'-and$_.enabled}|ForEach-Object {$_.map})
if($maps.Count-ne 3-or@($maps|Where-Object {$_-notmatch'^/Game/[A-Za-z0-9_/]+$'}).Count){throw 'Exactly three tracked maps required'}
$lease=[IO.File]::Open((Join-Path $root 'artifacts/offline-editor-runtime.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
$process=$null;$streams=@();$copies=@()
try{
 $dotnet='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/ThirdParty/DotNet/10.0/win-x64/dotnet.exe'
 $tool='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/DotNET/AutomationTool/AutomationTool.dll'
 $start=[Diagnostics.ProcessStartInfo]::new($dotnet);$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.WorkingDirectory='C:/Program Files/Epic Games/UE_5.8/Engine'
 $start.Environment['DOTNET_MULTILEVEL_LOOKUP']='0';$start.Environment['DOTNET_ROOT']=Split-Path $dotnet -Parent;$start.Environment['DOTNET_CLI_USE_MSBUILD_SERVER']='0'
 $arguments=@($tool,'BuildCookRun',('-project='+$descriptor),'-noP4','-platform=Win64','-clientconfig=Development','-skipbuild','-skipbuildeditor','-skipcook','-stage','-pak','-nodebuginfo',('-stagingdirectory='+$output+'/OfflineStage'),('-map='+($maps-join'+')),'-unattended','-utf8output','-ubtargs=-MaxParallelActions=1 -NoUBA','-AdditionalCookerOptions=-SkipZenStore -notraceserver -NoZenAutoLaunch -ddc=InstalledNoZenLocalFallback -UDPMESSAGING_TRANSPORT_ENABLE=0','-AdditionalPakOptions=-notraceserver -NoZenAutoLaunch')
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 $stamp=[guid]::NewGuid().ToString('N');$log=Join-Path $output ('audited-build-'+$stamp+'.log')
 $streams=@([IO.File]::Open($log,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite),[IO.File]::Open(($log+'.err'),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::ReadWrite))
 $process=[Diagnostics.Process]::Start($start);$copies=@($process.StandardOutput.BaseStream.CopyToAsync($streams[0]),$process.StandardError.BaseStream.CopyToAsync($streams[1]))
 @{status='RUNNING';pid=$process.Id;started=(Get-Date).ToString('o');log=$log;arguments=$arguments;preflight=$preflight;autoElevation=$false;firewallPolicyChanged=$false}|ConvertTo-Json -Depth 7|Set-Content (Join-Path $output 'audited-build-state.json')
 $elapsed=[Diagnostics.Stopwatch]::StartNew()
 while(!$process.WaitForExit(1000)){
  if((Get-PSDrive C).Free-lt500000000-or$elapsed.Elapsed.TotalMinutes-gt30){$process.Kill($true);$process.WaitForExit();throw 'Own private packaging stopped at disk/time limit'}
 }
 foreach($copy in $copies){[void]$copy.GetAwaiter().GetResult()}
 if($process.ExitCode-ne 0){throw ('Private packaging failed: '+$process.ExitCode+'; '+$log)}
 [void](Assert-SharedEditorFirewall -Executable $editor)
 $exe=Join-Path $output 'OfflineStage/Windows/RuntimeWorld/Binaries/Win64/RuntimeWorld.exe'
 if(!(Test-Path -LiteralPath $exe)){throw 'Package executable missing after successful build'}
 @{status='PASS';pid=$process.Id;exitCode=$process.ExitCode;finished=(Get-Date).ToString('o');log=$log;executableSha256=(Get-FileHash $exe).Hash;firewallPolicyChanged=$false}|ConvertTo-Json|Set-Content (Join-Path $output 'audited-build-state.json')
 Write-Output 'AUDITED_PRIVATE_UI_API_PACKAGE_BUILT'
}catch{
 @{status='FAIL';error=$_.Exception.Message;timestamp=(Get-Date).ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $output 'audited-build-failure.json');Copy-Item -LiteralPath (Join-Path $output 'audited-build-failure.json') -Destination (Join-Path $output 'audited-build-state.json') -Force;throw
}finally{
 if($process-and!$process.HasExited){$process.Kill($true);$process.WaitForExit()}
 foreach($copy in $copies){try{[void]$copy.GetAwaiter().GetResult()}catch{}}
 foreach($stream in $streams){$stream.Dispose()};$lease.Dispose()
}

