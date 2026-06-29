param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\dist"
)

$ErrorActionPreference = "Stop"
$solutionDir = Resolve-Path "$PSScriptRoot\.."
$outputPath = Resolve-Path $OutputDir

Write-Host "=== DeployKit Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Output: $outputPath"

# 1. Build solution
Write-Host "`n[1/4] Building solution..." -ForegroundColor Yellow
dotnet build "$solutionDir\DeployKit.sln" -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# 2. Pack NuGet
Write-Host "`n[2/4] Packing NuGet..." -ForegroundColor Yellow
$nugetDir = "$outputPath\nuget"
dotnet pack "$solutionDir\DeployKit.Integration\DeployKit.Integration.csproj" -c $Configuration -o $nugetDir -nologo
if ($LASTEXITCODE -ne 0) { throw "Pack failed" }

# 3. Publish GUI
Write-Host "`n[3/4] Publishing GUI..." -ForegroundColor Yellow
$guiDir = "$outputPath\DeployKit.Gui"
dotnet publish "$solutionDir\DeployKit.Gui\DeployKit.Gui.csproj" -c $Configuration -o $guiDir --self-contained false -nologo
if ($LASTEXITCODE -ne 0) { throw "GUI publish failed" }

# 4. Build Cloud API
Write-Host "`n[4/4] Publishing Cloud API..." -ForegroundColor Yellow
$apiDir = "$outputPath\DeployKit.Cloud.Api"
dotnet publish "$solutionDir\DeployKit.Cloud.Api\DeployKit.Cloud.Api.csproj" -c $Configuration -o $apiDir --self-contained false -nologo
if ($LASTEXITCODE -ne 0) { throw "API publish failed" }

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "NuGet:  $nugetDir\DeployKit.Integration.1.0.0.nupkg"
Write-Host "GUI:    $guiDir\DeployKit.Gui.exe"
Write-Host "API:    $apiDir\DeployKit.Cloud.Api.dll"
Write-Host "Size:   $(Get-ChildItem $guiDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1KB KB)"
