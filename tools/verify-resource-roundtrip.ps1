param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.0.83f1\Editor\Unity.exe',
    [string]$UnrealEditor = 'C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe'
)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$testRoot = Join-Path $workspace ('artifacts/resource-e2e/' + [guid]::NewGuid().ToString('N'))
$sourceProject = Join-Path $testRoot 'UnitySource'
$unrealTemplate = Join-Path $testRoot 'UnrealTemplate'
$unityTemplate = Join-Path $testRoot 'UnityTemplate'
$adapters = Join-Path $workspace 'adapters'
$unityVersion = (Get-Item -LiteralPath $UnityEditor).Directory.Parent.Name
foreach ($project in @($sourceProject,$unityTemplate)) {
    New-Item -ItemType Directory -Path (Join-Path $project 'Assets/Editor'),(Join-Path $project 'ProjectSettings'),(Join-Path $project 'Packages') -Force | Out-Null
    "m_EditorVersion: $unityVersion" | Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt')
    '{"dependencies":{}}' | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json')
}
Copy-Item -Path (Join-Path $adapters 'unity/*.cs') -Destination (Join-Path $sourceProject 'Assets/Editor')
New-Item -ItemType Directory -Path (Join-Path $unrealTemplate 'Content') -Force | Out-Null
'{"FileVersion":3,"EngineAssociation":"5.8"}' | Set-Content -LiteralPath (Join-Path $unrealTemplate 'PortTarget.uproject')
function Invoke-Native([string]$Executable, [string[]]$Arguments, [bool]$Capture = $false) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true; $start.WorkingDirectory = $workspace; $start.RedirectStandardOutput = $Capture
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($start)
    if ($Capture) { $read = $process.StandardOutput.ReadToEndAsync() }
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Native process failed: $Executable ($($process.ExitCode)); test logs: $testRoot" }
    if ($Capture) { return $read.GetAwaiter().GetResult() }
}
$seed = Join-Path $testRoot 'Seed'
Invoke-Native $UnityEditor @('-batchmode','-nographics','-quit','-projectPath',$sourceProject,'-executeMethod','EngineMigratorBridge.Run','-emMode','fixture','-emPackage',$seed,'-logFile',(Join-Path $testRoot 'fixture.log'))
$before = @{}
foreach ($folder in @('Assets','ProjectSettings','Packages')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $sourceProject $folder) -Recurse -File) { $before[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName).Hash }
}
$cli = Join-Path $workspace 'src/EngineMigrator.Cli/bin/Release/net9.0/EngineMigrator.Cli.dll'
$forward = (Invoke-Native 'dotnet' @($cli,'resources','Unity',$sourceProject,$unrealTemplate,$testRoot,$UnityEditor,$UnrealEditor,$adapters) $true) | ConvertFrom-Json
$reverse = (Invoke-Native 'dotnet' @($cli,'resources','Unreal',$forward.TargetCopy,$unityTemplate,$testRoot,$UnityEditor,$UnrealEditor,$adapters) $true) | ConvertFrom-Json
foreach ($file in $before.Keys) { if ((Get-FileHash -LiteralPath $file).Hash -ne $before[$file]) { throw "Original file changed: $file" } }
$originalPackage = Get-Content -LiteralPath (Join-Path $seed 'package.json') -Raw | ConvertFrom-Json
$reversePackage = Get-Content -LiteralPath (Join-Path $reverse.RunDirectory 'Transfer/package.json') -Raw | ConvertFrom-Json
function Geometry-Key($mesh) { return @($mesh.vertices | ForEach-Object { '{0:F4},{1:F4},{2:F4}' -f $_.x,$_.y,$_.z } | Sort-Object -Unique) -join ';' }
if ((Geometry-Key $originalPackage.meshes[0]) -ne (Geometry-Key $reversePackage.meshes[0])) { throw 'Roundtrip mesh positions differ' }
foreach ($texture in $originalPackage.textures) {
    $restored = $reversePackage.textures | Where-Object id -eq $texture.id
    foreach ($field in @('srgb','filter','wrapU','wrapV','mipmaps')) { if ($texture.$field -ne $restored.$field) { throw "Texture setting differs: $field" } }
}
foreach ($material in $originalPackage.materials) {
    $restored = $reversePackage.materials | Where-Object id -eq $material.id
    if ($material.shader -ne $restored.shader -or $material.texture -ne $restored.texture) { throw 'Material shader/texture differs' }
    if ($material.shader -eq 'standard') {
        foreach ($field in @('metallic','smoothness')) { if ([Math]::Abs($material.$field-$restored.$field) -gt 0.00001) { throw "PBR parameter differs: $field" } }
        foreach ($field in @('r','g','b','a')) { if ([Math]::Abs($material.color.$field-$restored.color.$field) -gt 0.00001) { throw "PBR color differs: $field" } }
    }
}
Add-Type -AssemblyName System.Drawing
$first = [System.Drawing.Bitmap]::new((Join-Path $seed $originalPackage.textures[0].file))
$last = [System.Drawing.Bitmap]::new((Join-Path (Join-Path $reverse.RunDirectory 'Transfer') $reversePackage.textures[0].file))
try {
    if ($first.Size -ne $last.Size) { throw 'Texture dimensions differ' }
    for ($x=0;$x -lt $first.Width;$x++) { for ($y=0;$y -lt $first.Height;$y++) { if ($first.GetPixel($x,$y).ToArgb() -ne $last.GetPixel($x,$y).ToArgb()) { throw 'Roundtrip texture pixel mismatch' } } }
} finally { $first.Dispose(); $last.Dispose() }
$result = [ordered]@{status='PASS';sourceUnchanged=$true;meshPositionsMatch=$true;texturePixelsMatch=$true;textureSettingsMatch=$true;pbrParametersMatch=$true;forward=$forward;reverse=$reverse;projectPortComplete=$false}
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $testRoot 'roundtrip-verification.json')
Write-Output "RESOURCE_ROUNDTRIP_PASS: $testRoot"
