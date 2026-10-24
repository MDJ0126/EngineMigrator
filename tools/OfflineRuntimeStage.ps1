. (Join-Path $PSScriptRoot 'Assert-PrivateRuntimeFirewall.ps1')

function Assert-OfflineRuntimePath([string]$Path){
    $allowed=[IO.Path]::GetFullPath((Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts'))+[IO.Path]::DirectorySeparatorChar
    $full=[IO.Path]::GetFullPath($Path)
    if(!$full.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Private artifact paths only'}
    for($candidate=$full;$candidate;$candidate=Split-Path $candidate -Parent){
        if((Test-Path -LiteralPath $candidate)-and((Get-Item -LiteralPath $candidate -Force).Attributes-band[IO.FileAttributes]::ReparsePoint)){throw 'Reparse paths are not allowed'}
    }
    if(Test-Path -LiteralPath $full -PathType Container){
        if(@(Get-ChildItem -LiteralPath $full -Recurse -Force|Where-Object {$_.Attributes-band[IO.FileAttributes]::ReparsePoint}).Count){throw 'Reparse entries are not allowed'}
    }
    return $full
}

function New-OfflineRuntimeStage {
    param([Parameter(Mandatory=$true)][string]$SourceWindows)
    $source=Assert-OfflineRuntimePath $SourceWindows
    $runner=Assert-OfflineRuntimePath (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/offline-runtime-runner')
    $windows=Assert-OfflineRuntimePath (Join-Path $runner 'Windows')
    $relativeExe='RuntimeWorld/Binaries/Win64/RuntimeWorld.exe'
    $executable=Join-Path $windows $relativeExe
    $rule=Assert-PrivateRuntimeFirewall -Executable $executable
    if(!(Test-Path -LiteralPath (Join-Path $source $relativeExe) -PathType Leaf)){throw 'Private packaged RuntimeWorld.exe is missing'}
    if($source.Equals($runner,[StringComparison]::OrdinalIgnoreCase)-or$source.StartsWith(($runner+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)-or$runner.StartsWith(($source+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)){throw 'The runner and source must be separate directory trees'}
    New-Item -ItemType Directory -Path $runner -Force|Out-Null
    $lease=[IO.File]::Open((Join-Path $runner 'runner.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    $incoming=Join-Path $runner ('incoming-'+[guid]::NewGuid().ToString('N'))
    $retired=Join-Path $runner ('retired-'+[guid]::NewGuid().ToString('N'))
    try{
        if(@(Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath -eq $executable}).Count){throw 'The private offline runtime is already running; it will not be terminated'}
        New-Item -ItemType Directory -Path $incoming|Out-Null
        $inventory=@(Get-ChildItem -LiteralPath $source -File -Recurse -Force|ForEach-Object {
            [pscustomobject]@{path=[IO.Path]::GetRelativePath($source,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
        })
        Get-ChildItem -LiteralPath $source -Force|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $incoming -Recurse -Force}
        foreach($file in $inventory){if((Get-FileHash -LiteralPath (Join-Path $incoming $file.path)).Hash-ne$file.sha256){throw ('Staged package hash differs: '+$file.path)}}
        if(@(Get-ChildItem -LiteralPath $incoming -File -Recurse -Force).Count-ne$inventory.Count){throw 'Staged file inventory differs'}
        if(Test-Path -LiteralPath $windows){Move-Item -LiteralPath $windows -Destination $retired}
        try{Move-Item -LiteralPath $incoming -Destination $windows}catch{if(Test-Path -LiteralPath $retired){Move-Item -LiteralPath $retired -Destination $windows};throw}
        if(Test-Path -LiteralPath $retired){$verified=Assert-OfflineRuntimePath $retired;Remove-Item -LiteralPath $verified -Recurse -Force}
        $manifest=[pscustomobject]@{sourceWindows=$source;executable=$executable;rule=$rule;timestamp=(Get-Date).ToString('o');files=$inventory;executableSha256=(Get-FileHash -LiteralPath $executable).Hash}
        $manifest|ConvertTo-Json -Depth 5|Set-Content (Join-Path $runner 'stage-manifest.json')
        return [pscustomobject]@{Executable=$executable;Lease=$lease;Manifest=$manifest}
    }catch{
        $lease.Dispose()
        if(Test-Path -LiteralPath $incoming){$verified=Assert-OfflineRuntimePath $incoming;Remove-Item -LiteralPath $verified -Recurse -Force}
        throw
    }
}
