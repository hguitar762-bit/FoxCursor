param(
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$output = Join-Path $root "artifacts/$Runtime"
$selfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
Push-Location $root
try {
    dotnet run --project tests/FoxCursor.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    dotnet publish src/FoxCursor/FoxCursor.csproj -c Release -r $Runtime --self-contained $selfContained -o $output -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
    dotnet publish src/FoxCursor.Guardian/FoxCursor.Guardian.csproj -c Release -r $Runtime --self-contained $selfContained -o $output -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'Guardian publish failed.' }
    Copy-Item README.md,LICENSE,ASSETS-LICENSE.md $output
    Copy-Item scripts/restore-cursor.ps1 $output
    New-Item -ItemType Directory -Path "$output/docs" -Force | Out-Null
    Copy-Item docs/character-preview.png,docs/WINDOWS-VALIDATION.md "$output/docs"
    $suffix = if ($FrameworkDependent) { '-framework-dependent' } else { '-portable' }
    $zip = Join-Path $root "artifacts/FoxCursor-0.1.0-$Runtime$suffix.zip"
    Compress-Archive -Path "$output/*" -DestinationPath $zip -Force
    Write-Host "Ready: $zip"
} finally { Pop-Location }
