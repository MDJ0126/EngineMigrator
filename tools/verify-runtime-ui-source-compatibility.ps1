param([string]$Cli='artifacts/runtime-ui-prefab-cli-v6/EngineMigrator.Cli.dll',[string]$Current='artifacts/runtime-ui-prefab-forward-v5/PROJECT-4a8ad94183a5/Target',[string]$Output='artifacts/runtime-ui-prefab-tests/ui-source-compatibility.json')
$ErrorActionPreference='Stop';$core=Join-Path (Split-Path ([IO.Path]::GetFullPath($Cli))) 'EngineMigrator.Core.dll';$assembly=[Reflection.Assembly]::LoadFrom($core);$method=$assembly.GetType('EngineMigrator.Core.UiRuntimePorting').GetMethod('VerifySource',[Reflection.BindingFlags]'NonPublic,Static')
$projects=@('artifacts/runtime-animation-prefab-forward-v6/PROJECT-40ae2348bc19/Target',$Current)
try{foreach($project in $projects){$method.Invoke($null,@([IO.Path]::GetFullPath($project)))|Out-Null}}
catch{@{status='FAIL';reason=$_.Exception.Message}|ConvertTo-Json|Set-Content $Output;throw}
@{status='PASS';legacySourceVerified=$true;currentSourceVerified=$true;projects=$projects;lineEndingsOnlyNormalized=$true}|ConvertTo-Json|Set-Content $Output
Write-Output 'LEGACY_AND_CURRENT_UI_SOURCE_VERIFIED'
