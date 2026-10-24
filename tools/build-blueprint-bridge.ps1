param([string]$EngineRoot='C:\Program Files\Epic Games\UE_5.8\Engine')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$package=Join-Path $workspace ('artifacts/blueprint-build/'+[guid]::NewGuid().ToString('N'))
& (Join-Path $EngineRoot 'Build/BatchFiles/RunUAT.bat') BuildPlugin ('-Plugin='+(Join-Path $workspace 'adapters/unreal/EMBlueprintBridge/EMBlueprintBridge.uplugin')) ('-Package='+$package) -TargetPlatforms=Win64
if($LASTEXITCODE-ne 0){throw "Native Blueprint bridge build failed: $package"}
$id=(Get-Content (Join-Path $EngineRoot 'Binaries/Win64/UnrealEditor.modules') -Raw|ConvertFrom-Json).BuildId
if($id-notmatch'^[A-Za-z0-9_-]+$'){throw 'Invalid engine build id'}
$variant=Join-Path $workspace "artifacts/blueprint-versions/$id/EMBlueprintBridge";$runtime=Join-Path $variant 'Binaries/Win64';New-Item -ItemType Directory -Path $runtime -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $package 'EMBlueprintBridge.uplugin') -Destination $variant -Force
foreach($name in @('UnrealEditor-EMBlueprintBridge.dll','UnrealEditor.modules')) { Copy-Item -LiteralPath (Join-Path $package ('Binaries/Win64/'+$name)) -Destination $runtime -Force }
Write-Output "BRIDGE_BUILD_PASS: $package; engine build $id; runtime $variant; rebuild EngineMigrator.App to include runtime"
