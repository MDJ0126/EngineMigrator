param([string]$Run='artifacts/runtime-order-forward-v1/PROJECT-e108194fcf03')
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);if(!$Run.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
$m=Get-Content ($Run+'/Target/.EngineMigratorProject.json') -Raw|ConvertFrom-Json
$cases=@(
 @{map='Phase';name='phase';comparer='compare-runtime-phase.py';expected='artifacts/runtime-phase-tests/unity-original.json'},
 @{map='Active';name='active';comparer='compare-runtime-active.py';expected='artifacts/runtime-active-tests/unity-original.json'},
 @{map='Enable';name='enable';comparer='compare-runtime-enable.py';expected='artifacts/runtime-enable-tests/unity-original.json'},
 @{map='Components';name='component';comparer='compare-runtime-components.py';expected='artifacts/runtime-component-tests/unity-original-v5.json'},
 @{map='Placement';name='placement';comparer='compare-runtime-placement.py';expected='artifacts/runtime-placement-tests/unity-original.json'},
 @{map='RuntimeStart';name='prefab';comparer='compare-runtime-prefab.py';expected='artifacts/runtime-prefab-tests/unity-original.json'}
)
foreach($case in $cases){$selected=@($m.units|Where-Object {$_.kind-eq'scene'-and$_.name-eq$case.map});if($selected.Count-ne 1){throw 'Select a unique tracked scene'};$output='artifacts/runtime-order-tests/'+$case.name+'-regression.json'
 & ($PSScriptRoot+'/capture-runtime-world-unreal.ps1') -Run $Run -Map $selected[0].map -Output $output
 & 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($PSScriptRoot+'/'+$case.comparer) $case.expected $output ('artifacts/runtime-order-tests/'+$case.name+'-regression-verification.json');if($LASTEXITCODE-ne 0){throw ('Runtime differs: '+$case.name)}
}
Write-Output 'RUNTIME_ORDER_REGRESSION_SUITE_PASS'
