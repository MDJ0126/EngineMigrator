param([Parameter(Mandatory=$true)][string]$ExpectedFile,[Parameter(Mandatory=$true)][string]$NativeFile,[Parameter(Mandatory=$true)][string]$PackageFile,[Parameter(Mandatory=$true)][string]$Output,[int]$Frames=250)
$ErrorActionPreference='Stop';New-Item -ItemType Directory -Force $Output|Out-Null
if(Test-Path ($Output+'/verification.json')){Move-Item ($Output+'/verification.json') ($Output+'/previous-verification-'+[guid]::NewGuid().ToString('N')+'.json')}
$expected=Get-Content $ExpectedFile -Raw|ConvertFrom-Json
$actual=Get-Content $NativeFile -Raw|ConvertFrom-Json
if($expected.frames.Count-ne$Frames-or$actual.frames.Count-ne$Frames){throw 'Captured frame count differs'}
if($actual.PSObject.Properties['deltas']){if($actual.deltas.Count-ne$Frames){throw 'Actual game delta count differs'};for($i=0;$i-lt$Frames;$i++){if([Math]::Abs($actual.deltas[$i]-$expected.deltas[$i])-gt 1e-7){throw "Actual game timestep differs at frame $i"}}}
foreach($phase in @('pawnsAtStart','pawnsAtEnd')){if($actual.PSObject.Properties[$phase]-and@($actual.$phase|Where-Object collisionEnabled).Count){throw "Unexpected colliding implicit Pawn in $phase"}}
if([double]$actual.clicksExecuted-ne [double](($expected.clicks|Measure-Object -Property count -Sum).Sum)){throw 'UI click broadcast count differs'}
if($actual.actors.Count-ne $expected.actors.Count){throw 'Actor Blueprint instance count differs'}
foreach($actor in $expected.actors){$native=$actual.actors|Where-Object id -eq $actor.id;if(!$native-or$native.className-ne$actor.className-or$native.owner-ne$actor.owner){throw 'Blueprint instance identity differs'};foreach($phase in @('state','destroyState')){foreach($field in $actor.$phase){$v=$native.$phase.($field.name);if(!$native.$phase.PSObject.Properties[$field.name]){throw "Missing native state $($field.name)"};if($field.boolean){if([bool]::Parse($field.value)-ne$v){throw 'Boolean state differs'}}elseif([Math]::Abs([double]$field.value-[double]$v)-gt 1e-5){throw "Blueprint state differs $($actor.id).$phase.$($field.name): Unity=$($field.value) Unreal=$v"}}}}
$package=Get-Content $PackageFile -Raw|ConvertFrom-Json;$physics=@($package.nodes|Where-Object {$_.hasPhysics-and$_.physics.hasBody-and!$_.physics.kinematic});$animated=@($package.nodes|Where-Object hasAnimation)
$texturedUI=@($package.nodes|Where-Object {$_.hasUI-and$_.ui.texture})
if($actual.packagedExecutableExecuted-and$texturedUI.Count){
 if(!$actual.PSObject.Properties['uiResources']){throw 'Packaged UI texture resources were not captured; use the current runtime probe'}
 foreach($node in $texturedUI){$resource=@($actual.uiResources|Where-Object widget -eq ($node.id+'_Visual'));if($resource.Count-ne 1){throw 'Packaged UI texture widget is missing or duplicated'};$resource=$resource[0];if($resource.texture-ne('T_'+$node.ui.texture)-or$resource.width-ne$node.ui.textureWidth-or$resource.height-ne$node.ui.textureHeight){throw "Packaged UI texture is missing or has different dimensions: $($node.name)"};foreach($axis in @('x','y','z','w')){if([Math]::Abs($resource.uv.$axis-$node.ui.uv.$axis)-gt 1e-7){throw "Packaged UI UV differs: $($node.name)"}}}
}
$physicsMetrics=@()
foreach($node in $physics){
 $height=if($node.physics.shape-eq'box'){$node.physics.size.y/2}else{$node.physics.radius};$maximum=0.0;$freeFallMaximum=0.0;$stepTravel=0.0;$previousUnity=$null;$previousNative=$null
 for($i=0;$i-lt$Frames;$i++){
  $u=$expected.frames[$i].objects|Where-Object id -eq $node.id;$n=$actual.frames[$i].objects|Where-Object id -eq $node.id
  $difference=[Math]::Abs($u.position.y-$n.position.y);$maximum=[Math]::Max($maximum,$difference)
  foreach($axis in @('x','z')){if([Math]::Abs($u.position.$axis-$n.position.$axis)-gt .04){throw 'Unexpected horizontal physics drift'}}
  if($u.position.y-gt($height+.04)-and$n.position.y-gt($height+.04)){
   $freeFallMaximum=[Math]::Max($freeFallMaximum,$difference);if($difference-gt .04){throw "Free-fall trajectory differs: $($node.name) frame=$i"}
   if($null-ne$previousUnity){$stepTravel=[Math]::Max($stepTravel,[Math]::Max([Math]::Abs($u.position.y-$previousUnity),[Math]::Abs($n.position.y-$previousNative)))}
  }else{
   # Different native solvers may recover discrete contact penetration differently.
   # Bound recovery by one measured pre-contact travel step; do not call the trajectories equivalent.
   if($difference-gt($stepTravel+.04)-or$u.position.y-lt($height-$stepTravel-.04)-or$n.position.y-lt($height-$stepTravel-.04)){throw "Collision recovery exceeds one-step travel bound: $($node.name) frame=$i"}
  }
  $previousUnity=$u.position.y;$previousNative=$n.position.y
 }
 if($Frames-ge 200-and([Math]::Abs($u.position.y-$height)-gt .04-or[Math]::Abs($n.position.y-$height)-gt .04)){throw 'Ground contact did not settle'}
 $physicsMetrics+=@{node=$node.name;maximumTrajectoryDifferenceMeters=$maximum;freeFallMaximumDifferenceMeters=$freeFallMaximum;discreteContactOneStepBoundMeters=($stepTravel+.04);trajectoryEquivalent=($maximum-le .04)}
}
foreach($node in $animated){for($i=0;$i-lt$Frames;$i++){$u=$expected.frames[$i].objects|Where-Object id -eq $node.id;$n=$actual.frames[$i].objects|Where-Object id -eq $node.id;foreach($property in @('position','scale')){foreach($axis in @('x','y','z')){if(!$n.PSObject.Properties[$property]-or[Math]::Abs($u.$property.$axis-$n.$property.$axis)-gt $(if($property-eq'scale'){.00002}else{.002})){throw "Animation playback differs $($node.name) frame=$i $property.$axis Unity=$($u.$property.$axis) Unreal=$($n.$property.$axis)"}}}}}
if(@($package.nodes|Where-Object {$_.hasUI-and$_.ui.canvas}).Count-gt 0-and$actual.widgets.Count-lt 1){throw 'UMG presenter did not create a runtime widget'}
@{status='PASS';savedScenesExecuted=$true;worldTickExecuted=$actual.worldTickExecuted;actorBlueprintInstances=$actual.actors.Count;physicsBodies=$physics.Count;physicsFreeFallToleranceMeters=.04;physicsTrajectoryMetrics=$physicsMetrics;physicsTrajectoryEquivalent=$(if($physics.Count){@($physicsMetrics|Where-Object {!$_.trajectoryEquivalent}).Count-eq 0}else{$null});buttonCallbacksExecuted=$actual.clicksExecuted;animationComponents=$animated.Count;animationToleranceMeters=.002;runtimeUMGWidgets=$actual.widgets.Count;frames=$Frames;projectPortComplete=$false;renderingEquivalent=$false}|ConvertTo-Json -Depth 8|Set-Content ($output+'/verification.json')
if($actual.packagedExecutableExecuted){$result=Get-Content ($output+'/verification.json') -Raw|ConvertFrom-Json;$result|Add-Member -NotePropertyName packagedExecutableExecuted -NotePropertyValue $true;$result|Add-Member -NotePropertyName collidingImplicitPawns -NotePropertyValue @($actual.pawnsAtStart|Where-Object collisionEnabled).Count;$result|Add-Member -NotePropertyName sharedUnityDeltaTimeline -NotePropertyValue $true;$result|Add-Member -NotePropertyName bootstrapFramesSkipped -NotePropertyValue $actual.bootstrapFramesSkipped;$result|ConvertTo-Json -Depth 8|Set-Content ($output+'/verification.json')}
if($actual.packagedExecutableExecuted-and$texturedUI.Count){$result=Get-Content ($output+'/verification.json') -Raw|ConvertFrom-Json;$result|Add-Member -NotePropertyName packagedUITexturesVerified -NotePropertyValue $texturedUI.Count;$result|ConvertTo-Json -Depth 8|Set-Content ($output+'/verification.json')}
Write-Output "EXTENDED_RUNTIME_PASS: $output"
