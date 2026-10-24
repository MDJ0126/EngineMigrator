$ErrorActionPreference = 'Stop'
$projectPath = Join-Path $PSScriptRoot 'src/EngineMigrator.App/EngineMigrator.App.csproj'
dotnet run --project $projectPath -c Release
if ($LASTEXITCODE -ne 0) { throw "EngineMigrator exited with code $LASTEXITCODE" }
