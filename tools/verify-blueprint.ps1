param(
 [string]$UnrealEditor='C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealEditor-Cmd.exe',
 [string]$BuiltPlugin=(Join-Path $PSScriptRoot '../artifacts/blueprint-plugin-5.8-compiled/HostProject/Plugins/EMBlueprintBridge')
)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop'
$workspace=Split-Path -Parent $PSScriptRoot
$run=Join-Path $workspace ('artifacts/blueprint-e2e/'+[guid]::NewGuid().ToString('N'))
$project=Join-Path $run 'Project'; $plugin=Join-Path $project 'Plugins/EMBlueprintBridge';$output=Join-Path $run 'Results'
New-Item -ItemType Directory -Path $plugin,$output -Force | Out-Null
foreach($entry in @('EMBlueprintBridge.uplugin','Binaries')) { Copy-Item -LiteralPath (Join-Path $BuiltPlugin $entry) -Destination $plugin -Recurse }
$engineVersion=Get-Content (Join-Path (Split-Path (Split-Path (Split-Path $UnrealEditor -Parent) -Parent) -Parent) 'Build/Build.version') -Raw|ConvertFrom-Json;@{FileVersion=3;EngineAssociation=("$($engineVersion.MajorVersion).$($engineVersion.MinorVersion)");Plugins=@(@{Name='PythonScriptPlugin';Enabled=$true},@{Name='EMBlueprintBridge';Enabled=$true})}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $project 'BlueprintPort.uproject')
$inputFile=Join-Path $workspace 'artifacts/blueprint-check/input.json'
@{input=$inputFile;output=$output} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'BlueprintRun.json')
$start=[Diagnostics.ProcessStartInfo]::new($UnrealEditor);$start.UseShellExecute=$false;$start.CreateNoWindow=$true
foreach($argument in @((Join-Path $project 'BlueprintPort.uproject'),'-run=pythonscript',('-script='+(Join-Path $workspace 'adapters/unreal/verify_blueprint.py')),'-nullrhi','-unattended','-nop4',('-abslog='+(Join-Path $run 'blueprint.log')))) {$start.ArgumentList.Add($argument)}
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw "Blueprint native execution failed: $run"}
# Reopen the saved .uasset in a separate editor process and extract its actual pins again.
$reloaded=Join-Path $run 'Reloaded';New-Item -ItemType Directory -Path $reloaded | Out-Null
$reloadInput=Join-Path $run 'reload-input.json';$reload=Get-Content -LiteralPath $inputFile -Raw | ConvertFrom-Json
$reload | Add-Member -NotePropertyName existingAsset -NotePropertyValue '/Game/EngineMigratorBlueprint/CombatMath.CombatMath'
$reload | ConvertTo-Json -Depth 60 | Set-Content -LiteralPath $reloadInput
@{input=$reloadInput;output=$reloaded} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'BlueprintRun.json')
$process=[Diagnostics.Process]::Start($start);$process.WaitForExit();if($process.ExitCode-ne 0){throw "Saved Blueprint reload failed: $run"}
$cli=Join-Path $workspace 'src/EngineMigrator.Cli/bin/Release/net9.0/EngineMigrator.Cli.dll'
$generated=Join-Path $output 'CombatMath.cs'
dotnet $cli blueprint-to-cs (Join-Path $reloaded 'blueprint-graph.json') $generated
if($LASTEXITCODE-ne 0){throw 'Graph conversion failed'}
dotnet $cli verify-blueprint (Join-Path $workspace 'artifacts/blueprint-check/CombatMath.original.cs') $generated $inputFile (Join-Path $reloaded 'blueprint-execution.json')
if($LASTEXITCODE-ne 0){throw 'Native result comparison failed'}
@{status='PASS';run=$run;source='C#';intermediate='Native compiled Blueprint';target='C#';savedAndReloaded=$true;cases=4;projectPortComplete=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'verification.json')
Write-Output "BLUEPRINT_ROUNDTRIP_PASS: $run"
