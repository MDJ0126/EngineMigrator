param([Parameter(Mandatory=$true)][string]$Run,[Parameter(Mandatory=$true)][string]$Output,[string]$Scene='UIApi',[switch]$Plan)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineRuntimeStage.ps1')
. (Join-Path $PSScriptRoot 'Assert-SharedEditorFirewall.ps1')
$Run=Assert-OfflineRuntimePath $Run;$Output=Assert-OfflineRuntimePath $Output
if(Test-Path -LiteralPath $Output){throw 'A fresh capture output is required'}
$descriptor=Join-Path $Run 'Target/RuntimeWorld.uproject'
if(!(Test-Path -LiteralPath $descriptor)){throw 'Existing private runtime project required'}
$manifest=Get-Content (Join-Path $Run 'Target/.EngineMigratorProject.json') -Raw|ConvertFrom-Json
$selected=@($manifest.units|Where-Object {$_.kind-eq'scene'-and$_.name-eq$Scene})
if($selected.Count-ne 1-or$selected[0].map-notmatch'^/Game/[A-Za-z0-9_/]+$'){throw 'Select one tracked scene'}
$exe='C:/Program Files/Epic Games/UE_5.8/Engine/Binaries/Win64/UnrealEditor-Cmd.exe'
$arguments=@($descriptor,$selected[0].map,'-game','-nullrhi','-RenderOffscreen','-nosound','-unattended','-nop4','-notraceserver','-NoZenAutoLaunch','-ddc=InstalledNoZenLocalFallback','-UDPMESSAGING_TRANSPORT_ENABLE=0','-UseFixedTimeStep','-FPS=50',('-EMRuntimeProbe='+$Output),('-abslog='+$Output+'.log'))
$preflight=Assert-SharedEditorFirewall -Executable $exe
if($Plan){[pscustomobject]@{executable=$exe;arguments=$arguments;preflight=$preflight;engineStarted=$false;policyChanged=$false}|ConvertTo-Json -Depth 7;return}
# Default execution remains blocked. This prepared launcher has no permission to bypass the incident restriction.
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')
throw 'Execution implementation is intentionally unavailable pending explicit shared-engine approval.'
