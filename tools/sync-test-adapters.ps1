param([Parameter(Mandatory=$true)][string]$Project)
$ErrorActionPreference='Stop';$workspace=Split-Path -Parent $PSScriptRoot;$Project=[IO.Path]::GetFullPath($Project)
if(!$Project.StartsWith(($workspace+'/artifacts/').Replace('/','\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Fixture adapter installation is restricted to private artifacts'}
$folder=Join-Path $Project 'Assets/Editor';New-Item -ItemType Directory -Force $folder|Out-Null
$hashes=@{};foreach($file in Get-ChildItem ($workspace+'/adapters/unity') -Filter '*.cs'){
 Copy-Item -LiteralPath $file.FullName -Destination $folder -Force
 $hashes[$file.Name]=(Get-FileHash -LiteralPath (Join-Path $folder $file.Name) -Algorithm SHA256).Hash
}
$hashes|ConvertTo-Json|Set-Content (Join-Path $folder '.EngineMigratorAdapterHashes.json')
Write-Output "PRIVATE_TEST_ADAPTERS_SYNCED: $Project"
