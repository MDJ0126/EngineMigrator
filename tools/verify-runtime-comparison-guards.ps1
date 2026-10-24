param([string]$Run='artifacts/ui-sprite-tests/RP-6c891568180e',[string]$Native='artifacts/packaging-tests/UE58Sprite/PackagedRuntime/scene-execution.json')
$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$root=Join-Path $workspace 'artifacts/runtime-comparison-guards';New-Item -ItemType Directory -Force $root|Out-Null
$results=@();foreach($kind in @('missingCapture','wrongTexture','wrongUV','implicitPawn')){
 $data=Get-Content $Native -Raw|ConvertFrom-Json
 switch($kind){
  'missingCapture'{$data.PSObject.Properties.Remove('uiResources');$expected='resources were not captured'}
  'wrongTexture'{($data.uiResources|Where-Object texture|Select-Object -First 1).texture='MissingCookedTexture';$expected='texture is missing'}
  'wrongUV'{($data.uiResources|Where-Object texture|Select-Object -First 1).uv.x+=.125;$expected='UI UV differs'}
  'implicitPawn'{$data.pawnsAtStart=@([pscustomobject]@{class='/Script/Engine.DefaultPawn';collisionEnabled=$true});$expected='Unexpected colliding implicit Pawn'}
 }
 $file=Join-Path $root ($kind+'.json');$output=Join-Path $root $kind;$data|ConvertTo-Json -Depth 30 -Compress|Set-Content $file;$rejected=$false
 try{& ($PSScriptRoot+'/compare-runtime-results.ps1') -ExpectedFile ($Run+'/ExtendedRuntime-250/unity-execution.json') -NativeFile $file -PackageFile ($Run+'/Transfer/package.json') -Output $output}catch{if($_.Exception.Message-notlike('*'+$expected+'*')){throw};$rejected=$true}
 if(!$rejected-or(Test-Path ($output+'/verification.json'))){throw "Invalid runtime capture was incorrectly accepted: $kind"}
 $results+=@{case=$kind;rejected=$true;passResultAbsent=$true}
}
@{status='PASS';guards=$results;syntheticCorruptionOfActualRuntimeCapture=$true}|ConvertTo-Json -Depth 6|Set-Content ($root+'/verification.json');Write-Output 'RUNTIME_COMPARISON_GUARDS_PASS'
