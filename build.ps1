#requires -Version 7.0
<#
    Build script for Axiomate Windows x64 installer.

    Steps:
      1. Read version.json (single source of truth).
      2. Sync axiomate dist/ from the upstream agent build into Resources/dist/.
      3. Verify required payloads (Git/Python installers) exist.
      4. Publish AxiomateUninstaller.exe and stash it as Resources/Uninstaller.exe so
         the main installer can embed it.
      5. Publish AxiomateInstaller.exe (single-file, self-contained, win-x64).
      6. Rename the produced exe with the installer version and report final path/size.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$AxiomateDistSource = "C:\public\workspace\axiomate\agent\dist",
    [switch]$SkipDistSync,
    [switch]$KeepArtifactsRaw
)

$ErrorActionPreference = "Stop"
$ProgressPreference   = "SilentlyContinue"

$Root            = Split-Path -Parent $MyInvocation.MyCommand.Path
$VersionFile     = Join-Path $Root "version.json"
$InstallerProj   = Join-Path $Root "src\AxiomateInstaller\AxiomateInstaller.csproj"
$UninstallerProj = Join-Path $Root "src\AxiomateUninstaller\AxiomateUninstaller.csproj"
$ResourcesDir    = Join-Path $Root "src\AxiomateInstaller\Resources"
$DistDest        = Join-Path $ResourcesDir "dist"
$ArtifactsDir    = Join-Path $Root "artifacts"
$InstallerOut    = Join-Path $ArtifactsDir "installer"
# Uninstaller is just an intermediate: build.ps1 publishes it, then embeds it
# into the main installer EXE. Keep it under _intermediate so users don't
# mistake it for a separate deliverable.
$UninstallerOut  = Join-Path $ArtifactsDir "_intermediate\uninstaller"

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Info([string]$msg) { Write-Host "    $msg" -ForegroundColor DarkGray }

# ---------- 1. Versions ----------
Write-Step "Reading version.json"
if (-not (Test-Path $VersionFile)) { throw "version.json missing at $VersionFile" }
$ver = Get-Content -Raw -Encoding UTF8 $VersionFile | ConvertFrom-Json
$installerVersion = [string]$ver.installerVersion
$axiomateVersion  = [string]$ver.axiomateVersion
if ([string]::IsNullOrWhiteSpace($installerVersion)) { throw "installerVersion missing in version.json" }
Write-Info "installerVersion = $installerVersion"
Write-Info "axiomateVersion  = $axiomateVersion (raw)"

# ---------- 2. Sync axiomate dist ----------
if (-not $SkipDistSync) {
    Write-Step "Syncing axiomate dist from $AxiomateDistSource"
    if (-not (Test-Path $AxiomateDistSource)) {
        throw "Axiomate dist source not found: $AxiomateDistSource. Build axiomate first or pass -AxiomateDistSource."
    }
    if (Test-Path $DistDest) { Remove-Item -Recurse -Force $DistDest }
    New-Item -ItemType Directory -Force -Path $DistDest | Out-Null
    Copy-Item -Recurse -Force "$AxiomateDistSource\*" $DistDest
    $distSize = (Get-ChildItem -Recurse $DistDest | Measure-Object -Property Length -Sum).Sum
    Write-Info ("dist synced: {0:N0} bytes across {1} files" -f $distSize, (Get-ChildItem -Recurse -File $DistDest).Count)
} else {
    Write-Step "SkipDistSync = true (using existing $DistDest)"
}

# resolve "auto" axiomate version from axiomate.exe FileVersion
if ($axiomateVersion -eq "auto") {
    $axiomateExe = Join-Path $DistDest "axiomate.exe"
    if (Test-Path $axiomateExe) {
        $fv = (Get-Item $axiomateExe).VersionInfo.FileVersion
        if ([string]::IsNullOrWhiteSpace($fv)) { $fv = "unknown" }
        $axiomateVersion = $fv
    } else {
        $axiomateVersion = "unknown"
    }
    Write-Info "axiomateVersion (resolved) = $axiomateVersion"
}

# ---------- 3. Payload sanity check ----------
Write-Step "Verifying payloads"
$requiredPayloads = @("Git-2.54.0-64-bit.exe", "python-3.12.10-amd64.exe")
foreach ($p in $requiredPayloads) {
    $full = Join-Path $ResourcesDir $p
    if (-not (Test-Path $full)) { throw "Missing payload: $full" }
    $sizeMb = [math]::Round((Get-Item $full).Length / 1MB, 1)
    Write-Info ("OK  {0}  ({1} MB)" -f $p, $sizeMb)
}

# ---------- 4. Publish AxiomateUninstaller ----------
Write-Step "Publishing AxiomateUninstaller"
if (Test-Path $UninstallerOut) { Remove-Item -Recurse -Force $UninstallerOut }
$uninstallerInfo = "$installerVersion+axiomate.$axiomateVersion"
& dotnet publish $UninstallerProj `
    -c $Configuration `
    -r win-x64 `
    -o $UninstallerOut `
    "-p:Version=$installerVersion" `
    "-p:FileVersion=$installerVersion.0" `
    "-p:InformationalVersion=$uninstallerInfo" `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Uninstaller publish failed (exit $LASTEXITCODE)" }

$uninstallerExe = Join-Path $UninstallerOut "axiomate-uninstaller.exe"
if (-not (Test-Path $uninstallerExe)) { throw "Expected uninstaller exe missing: $uninstallerExe" }
Copy-Item $uninstallerExe (Join-Path $ResourcesDir "Uninstaller.exe") -Force
Write-Info ("embedded Uninstaller.exe ({0:N0} bytes)" -f (Get-Item $uninstallerExe).Length)

# ---------- 5. Publish main installer ----------
Write-Step "Publishing AxiomateInstaller (this can take a while because of the embedded payloads)"
if (Test-Path $InstallerOut) { Remove-Item -Recurse -Force $InstallerOut }
$installerInfo = "$installerVersion+axiomate.$axiomateVersion"
& dotnet publish $InstallerProj `
    -c $Configuration `
    -r win-x64 `
    -o $InstallerOut `
    "-p:Version=$installerVersion" `
    "-p:FileVersion=$installerVersion.0" `
    "-p:InformationalVersion=$installerInfo" `
    --nologo
if ($LASTEXITCODE -ne 0) { throw "Installer publish failed (exit $LASTEXITCODE)" }

# ---------- 6. Rename + report ----------
$rawExe   = Join-Path $InstallerOut "axiomate-installer.exe"
$finalExe = Join-Path $InstallerOut ("axiomate-installer-{0}.exe" -f $installerVersion)
if (-not (Test-Path $rawExe)) { throw "Expected installer exe missing: $rawExe" }
if ((Test-Path $finalExe) -and (-not $KeepArtifactsRaw)) { Remove-Item -Force $finalExe }
if (-not $KeepArtifactsRaw) {
    Move-Item $rawExe $finalExe -Force
} else {
    Copy-Item $rawExe $finalExe -Force
}

$bytes = (Get-Item $finalExe).Length

# Clean up intermediate artifacts so only the deliverable is left.
$intermediateDir = Join-Path $ArtifactsDir "_intermediate"
if (Test-Path $intermediateDir) { Remove-Item -Recurse -Force $intermediateDir }
# Remove pdb / version.json side-cars next to the final exe so the installer/
# folder contains exactly one file: the installer.
foreach ($side in @("axiomate-installer.pdb", "version.json")) {
    $p = Join-Path $InstallerOut $side
    if (Test-Path $p) { Remove-Item -Force $p }
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Green
Write-Host (" axiomate-installer  v{0}" -f $installerVersion) -ForegroundColor Green
Write-Host (" bundled axiomate    v{0}" -f $axiomateVersion)  -ForegroundColor Green
Write-Host (" output:             {0}" -f $finalExe)          -ForegroundColor Green
Write-Host (" size:               {0:N1} MB ({1:N0} bytes)" -f ($bytes/1MB), $bytes) -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
