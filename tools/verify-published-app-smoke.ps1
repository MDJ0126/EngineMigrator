param([Parameter(Mandatory=$true)][string]$ArchiveVerification,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop';$allowed=[IO.Path]::GetFullPath('artifacts')+'\';$archive=Get-Content $ArchiveVerification -Raw|ConvertFrom-Json
if($archive.status-ne'PASS'){throw 'Archive verification required'}
$path=[IO.Path]::GetFullPath($archive.unicodeExtraction);if(!$path.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)-or![IO.Path]::GetFullPath($Output).StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private artifacts only'}
$exe=$path+'/EngineMigrator.App.exe';$p=Start-Process -FilePath $exe -ArgumentList '--smoke-test' -WorkingDirectory $path -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode-ne 0){throw 'Published app smoke failed'}
$result=Get-Content ($path+'/artifacts/studio-checks.txt') -Raw;if(!$result.StartsWith('PASS ')){throw 'Published app smoke evidence missing'}
@{status='PASS';executableSha256=(Get-FileHash $exe).Hash;computerUse=$false;unicodePath=$true;evidence=($path+'/artifacts/studio-checks.txt')}|ConvertTo-Json|Set-Content $Output -Encoding utf8
Write-Output 'PUBLISHED_APP_UNICODE_SMOKE_PASS'
