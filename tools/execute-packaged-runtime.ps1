param([Parameter(Mandatory=$true)][string]$Packaging,[Parameter(Mandatory=$true)][string]$Comparer,[Parameter(Mandatory=$true)][string]$Expected)
# This wrapper launches exclusively through the fixed-path firewall-verified offline runner.

$ErrorActionPreference='Stop';$Packaging=[IO.Path]::GetFullPath($Packaging);$allowed=[IO.Path]::GetFullPath('artifacts')+'\';if(!$Packaging.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)-or![IO.Path]::GetFullPath($Expected).StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private artifacts only'}
if($Comparer-notmatch'^compare-runtime-[a-z-]+\.py$'){throw 'Select a runtime comparer'}
$execution=& (Join-Path $PSScriptRoot 'execute-offline-runtime.ps1') -SourceWindows ($Packaging+'/Archive/Windows') -Output $Packaging
$capture=$execution.capture
& 'C:/Users/ehdwn/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' ($PSScriptRoot+'/'+$Comparer) $Expected $capture ($Packaging+'/verification.json')
if($LASTEXITCODE-ne 0){throw 'Packaged execution differs'}
$verified=Get-Content ($Packaging+'/verification.json') -Raw|ConvertFrom-Json
foreach($property in @('capture','executable','executableSha256','packagedExecutableExecuted','rule')){$verified|Add-Member -NotePropertyName $property -NotePropertyValue $execution.$property -Force}
$verified|ConvertTo-Json -Depth 14|Set-Content ($Packaging+'/verification.json') -Encoding utf8
Write-Output 'PACKAGED_RUNTIME_FRESH_EXECUTION_PASS'

