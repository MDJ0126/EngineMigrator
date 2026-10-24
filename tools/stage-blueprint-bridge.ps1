param([Parameter(Mandatory=$true)][string]$BuiltPlugin)
$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$BuiltPlugin=[IO.Path]::GetFullPath($BuiltPlugin)
if(!$BuiltPlugin.StartsWith(([IO.Path]::GetFullPath($workspace)+'/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Only workspace plugin builds can be staged'}
$manifest=Join-Path $BuiltPlugin 'Binaries/Win64/UnrealEditor.modules';$id=(Get-Content $manifest -Raw|ConvertFrom-Json).BuildId
if($id-notmatch'^[A-Za-z0-9_-]+$'){throw 'Invalid engine build ID'}
$targets=@((Join-Path $workspace "adapters/unreal/native/$id/EMBlueprintBridge"),(Join-Path $workspace "artifacts/blueprint-versions/$id/EMBlueprintBridge"))
foreach($target in $targets){New-Item -ItemType Directory -Path ($target+'/Binaries/Win64') -Force|Out-Null;Copy-Item ($BuiltPlugin+'/EMBlueprintBridge.uplugin') $target -Force;foreach($name in @('UnrealEditor.modules','UnrealEditor-EMBlueprintBridge.dll')){Copy-Item ($BuiltPlugin+'/Binaries/Win64/'+$name) ($target+'/Binaries/Win64') -Force}}
foreach($target in $targets){if(Test-Path ($BuiltPlugin+'/Source')){Copy-Item ($BuiltPlugin+'/Source') $target -Recurse -Force}}
$hash=(Get-FileHash ($BuiltPlugin+'/Binaries/Win64/UnrealEditor-EMBlueprintBridge.dll')).Hash
foreach($target in $targets){if((Get-FileHash ($target+'/Binaries/Win64/UnrealEditor-EMBlueprintBridge.dll')).Hash-ne$hash){throw 'Staged module hash mismatch'}}
$fallback=Join-Path $workspace 'artifacts/blueprint-runtime/Win64';$fallbackManifest=Join-Path $fallback 'UnrealEditor.modules'
if((Test-Path $fallbackManifest)-and((Get-Content $fallbackManifest -Raw|ConvertFrom-Json).BuildId-eq$id)){foreach($name in @('UnrealEditor.modules','UnrealEditor-EMBlueprintBridge.dll')){Copy-Item ($BuiltPlugin+'/Binaries/Win64/'+$name) $fallback -Force};if((Get-FileHash ($fallback+'/UnrealEditor-EMBlueprintBridge.dll')).Hash-ne$hash){throw 'Fallback module hash mismatch'}}
@{status='PASS';engineBuild=$id;sha256=$hash;copies=$targets}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $workspace "artifacts/native-stage-$id.json")
Write-Output "NATIVE_BRIDGE_STAGED: $id"
