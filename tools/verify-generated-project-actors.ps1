param([Parameter(Mandatory=$true)][string]$Run,[Parameter(Mandatory=$true)][string]$Cli,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop';$allowed=[IO.Path]::GetFullPath('artifacts')+'\'
foreach($path in @($Run,$Output)){if(![IO.Path]::GetFullPath($path).StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private artifacts only'}}
$scripts=@{};$manifest=Get-Content ($Run+'/Transfer/project-package.json') -Raw|ConvertFrom-Json
foreach($unit in $manifest.units){$data=Get-Content ($Run+'/Transfer/'+$unit.package+'/package.json') -Raw|ConvertFrom-Json;foreach($script in $data.scripts){if($scripts.ContainsKey($script.name)-and$scripts[$script.name]-cne$script.source){throw 'Conflicting script sources'};$scripts[$script.name]=$script.source}}
New-Item $Output -ItemType Directory -Force|Out-Null
$source="using UnityEngine;`nusing UnityEngine.UI;`nusing UnityEngine.SceneManagement;`n"+(($scripts.Values|ForEach-Object {$_ -replace 'using UnityEngine(?:\.SceneManagement|\.UI)?;',''}) -join "`n")
$sourceFile=$Output+'/source.cs';Set-Content $sourceFile $source
& dotnet $Cli actors-to-cpp $sourceFile ($Output+'/headers');if($LASTEXITCODE-ne 0){throw 'Generation failed'}
$actual=$Run+'/Target/Plugins/EMActorPort/Source/EMActorPort/Public';$files=@(Get-ChildItem ($Output+'/headers') -Filter '*.h');if($files.Count-ne@(Get-ChildItem $actual -Filter '*.h').Count){throw 'Header count mismatch'}
$hashes=@{};foreach($file in $files){$hash=(Get-FileHash $file.FullName).Hash;if($hash-ne(Get-FileHash ($actual+'/'+$file.Name)).Hash){throw ('Executed generated header differs: '+$file.Name)};$hashes[$file.Name]=$hash}
@{status='PASS';headersCompared=$files.Count;hashes=$hashes}|ConvertTo-Json -Depth 5|Set-Content ($Output+'/verification.json') -Encoding utf8
Write-Output 'GENERATED_PROJECT_ACTOR_HEADERS_MATCH_EXECUTED'
