param([Parameter(Mandatory=$true)][string]$Run)
. (Join-Path $PSScriptRoot 'Assert-EngineExecutionAllowed.ps1')

$ErrorActionPreference='Stop';$Run=[IO.Path]::GetFullPath($Run);if(!$Run.StartsWith(([IO.Path]::GetFullPath('artifacts')+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Private fixture only'}
$m=Get-Content ($Run+'/Target/.EngineMigratorProject.json') -Raw|ConvertFrom-Json
$cases=@(
 @{map='AnimationPrefab';name='animation-prefab';comparer='compare-runtime-animation-prefab.py';expected='artifacts/runtime-animation-prefab-tests/unity-original.json'},
 @{map='PhysicsPrefab';name='physics-prefab';comparer='compare-runtime-physics-prefab.py';expected='artifacts/runtime-physics-prefab-tests/unity-original.json'},
 @{map='Dependency';name='dependency';comparer='compare-runtime-dependency.py';expected='artifacts/runtime-dependency-tests/unity-original.json'},
 @{map='Dormant';name='dormant';comparer='compare-runtime-dormant.py';expected='artifacts/runtime-dormant-tests/unity-original.json'},
 @{map='Destroy';name='destroy';comparer='compare-runtime-destroy.py';expected='artifacts/runtime-destroy-tests/unity-original.json'},
 @{map='Order';name='order';comparer='compare-runtime-order.py';expected='artifacts/runtime-order-tests/unity-original.json'},
 @{map='Phase';name='phase';comparer='compare-runtime-phase.py';expected='artifacts/runtime-phase-tests/unity-original.json'},
 @{map='Active';name='active';comparer='compare-runtime-active.py';expected='artifacts/runtime-active-tests/unity-original.json'},
 @{map='Enable';name='enable';comparer='compare-runtime-enable.py';expected='artifacts/runtime-enable-tests/unity-original.json'},
 @{map='Components';name='component';comparer='compare-runtime-components.py';expected='artifacts/runtime-component-tests/unity-original-v5.json'},
 @{map='Placement';name='placement';comparer='compare-runtime-placement.py';expected='artifacts/runtime-placement-tests/unity-original.json'},
 @{map='RuntimeStart';name='prefab';comparer='compare-runtime-prefab.py';expected='artifacts/runtime-prefab-tests/unity-original.json'}
)
foreach($case in $cases){$selected=@($m.units|Where-Object {$_.kind-eq'scene'-and$_.name-eq$case.map});if($selected.Count-ne 1){throw 'Select a unique tracked scene'};$output='artifacts/runtime-ui-prefab-tests/'+$case.name+'-regression.json'
 & ($PSScriptRoot+'/capture-runtime-world-unreal.ps1') -Run $Run -Map $selected[0].map -Output $output
 & 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($PSScriptRoot+'/'+$case.comparer) $case.expected $output ('artifacts/runtime-ui-prefab-tests/'+$case.name+'-regression-verification.json');if($LASTEXITCODE-ne 0){throw ('Runtime differs: '+$case.name)}
}
Write-Output 'RUNTIME_UI_PREFAB_REGRESSION_SUITE_PASS'








