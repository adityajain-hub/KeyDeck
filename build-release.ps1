<#
.SYNOPSIS
    Builds a standalone, zero-dependency, single-file KeyDeck.exe for Windows 10/11 x64.
.DESCRIPTION
    Compiles KeyDeck with .NET 8, bundles the full desktop runtime, native dependencies,
    and single-file compression into publish/KeyDeck.exe.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Write-Host "====================================================================" -ForegroundColor Cyan
Write-Host " KeyDeck Standalone Executable Publisher (.NET 8 Windows x64)" -ForegroundColor Cyan
Write-Host "====================================================================" -ForegroundColor Cyan

$projectDir = Join-Path $PSScriptRoot "src\KeyDeck\KeyDeck.csproj"
$outputDir  = Join-Path $PSScriptRoot "publish"

Write-Host "`n[1/2] Compiling and packaging single-file executable..." -ForegroundColor Yellow

dotnet publish $projectDir `
    -r win-x64 `
    -c Release `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $outputDir

if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $outputDir "KeyDeck.exe"
    if (Test-Path $exePath) {
        $sizeMb = [Math]::Round(((Get-Item $exePath).Length / 1MB), 2)
        Write-Host "`n[2/2] Build Succeeded!" -ForegroundColor Green
        Write-Host "  Executable: $exePath" -ForegroundColor White
        Write-Host "  File Size:  $sizeMb MB (Self-Contained + Compressed)" -ForegroundColor White
        Write-Host "`n✨ Ready to run! No .NET installation required on target PCs." -ForegroundColor Green
    }
} else {
    Write-Host "`n❌ Publish failed with exit code $LASTEXITCODE." -ForegroundColor Red
    exit $LASTEXITCODE
}
