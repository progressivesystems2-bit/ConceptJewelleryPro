<#
.SYNOPSIS
    Builds, packages, and optionally publishes a GitHub Release for Concept Jewellery Pro (WPF).
.EXAMPLE
    .\publish-wpf-release.ps1 -Version "1.0.1" -Notes "Bug fixes and performance improvements"
.EXAMPLE
    .\publish-wpf-release.ps1 -Version "1.0.1" -Notes "Release notes" -UploadToGitHub
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Notes = "Concept Jewellery Pro release.",

    [switch]$SelfContained = $false,

    [switch]$UploadToGitHub = $false,

    [switch]$PreRelease = $false
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$wpfProj = Join-Path $repoRoot "WPF\ConceptJewelleryPro.WPF.csproj"
$releaseRoot = Join-Path $repoRoot "releases"
$publishDir = Join-Path $releaseRoot "staging-v$Version"
$zipName = "ConceptJewelleryPro-WPF-v$Version-win-x64.zip"
$zipPath = Join-Path $releaseRoot $zipName
$shaPath = "$zipPath.sha256"

Write-Host "=== Concept Jewellery Pro WPF Release Builder (v$Version) ===" -ForegroundColor Cyan

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

# Build & publish with the requested version stamped into the assembly
$selfContainedArg = if ($SelfContained) { "true" } else { "false" }
Write-Host "Publishing WPF project (SelfContained=$selfContainedArg)..." -ForegroundColor Yellow

dotnet publish $wpfProj `
    -c Release `
    -r win-x64 `
    --self-contained $selfContainedArg `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

# Remove local secrets, databases, and debug symbols from the update package
Write-Host "Sanitizing release payload (excluding appsettings.json, *.mdb, *.db, *.pdb)..." -ForegroundColor Yellow
Get-ChildItem -Path $publishDir -Recurse -File | Where-Object {
    $_.Name -ieq "appsettings.json" -or
    $_.Extension -in @(".mdb", ".accdb", ".ldb", ".db", ".pdb", ".log")
} | Remove-Item -Force -ErrorAction SilentlyContinue

# Create ZIP archive
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Write-Host "Creating update package: $zipPath" -ForegroundColor Yellow
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

# Compute SHA-256 checksum
$hash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" | Set-Content -Path $shaPath -Encoding ASCII

Write-Host ""
Write-Host "Package created successfully!" -ForegroundColor Green
Write-Host "  ZIP:    $zipPath"
Write-Host "  SHA256: $hash"

if ($UploadToGitHub) {
    $tag = "v$Version"
    Write-Host ""
    Write-Host "Publishing GitHub Release $tag..." -ForegroundColor Cyan

    $ghArgs = @("release", "create", $tag, $zipPath, $shaPath, "--title", "Concept Jewellery Pro v$Version", "--notes", $Notes)
    if ($PreRelease) {
        $ghArgs += "--prerelease"
    }

    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) {
        throw "gh release create failed with exit code $LASTEXITCODE"
    }

    Write-Host "GitHub Release $tag published! Users can now update from Settings -> Software Updates." -ForegroundColor Green
}

# Clean up staging folder
Remove-Item $publishDir -Recurse -Force -ErrorAction SilentlyContinue
