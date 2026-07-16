$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'MDZipViewer.csproj'

Write-Host 'Checking .NET SDK...'
dotnet --info

Write-Host 'Building MDZipViewer...'
dotnet build $project -c Release

Write-Host 'Launching MDZipViewer...'
dotnet run --project $project -c Release -- @args
