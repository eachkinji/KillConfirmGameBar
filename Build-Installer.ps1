param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$MsBuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
    [string]$InnoCompilerPath = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ManifestPath = Join-Path $Root "Package\Package.appxmanifest"
$InstallerScript = Join-Path $Root "Installer\KillConfirmGameBar.iss"

if (-not (Test-Path $ManifestPath)) {
    throw "Package.appxmanifest was not found at $ManifestPath"
}

if (-not (Test-Path $InstallerScript)) {
    throw "Installer script was not found at $InstallerScript"
}

[xml]$Manifest = Get-Content $ManifestPath
$Version = $Manifest.Package.Identity.Version
if (-not $Version) {
    throw "Could not read package version from $ManifestPath"
}

$TransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}" -f $Version)

& (Join-Path $Root "Build-TransferPackage.ps1") -Configuration $Configuration -Platform $Platform -MsBuildPath $MsBuildPath

if (-not (Test-Path $TransferRoot)) {
    throw "Expected transfer folder was not produced: $TransferRoot"
}

if (-not $InnoCompilerPath) {
    $Inno = Get-Command iscc -ErrorAction SilentlyContinue
    if ($Inno) {
        $InnoCompilerPath = $Inno.Source
    }
    else {
        $Candidates = @(
            (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
            "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
            "C:\Program Files\Inno Setup 6\ISCC.exe"
        )
        $InnoCompilerPath = $Candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}

if (-not $InnoCompilerPath -or -not (Test-Path $InnoCompilerPath)) {
    throw "Inno Setup compiler was not found. Install Inno Setup 6, then run this script again."
}

New-Item -ItemType Directory -Force -Path (Join-Path $Root "Output") | Out-Null

& $InnoCompilerPath `
    ("/DMyAppVersion={0}" -f $Version) `
    ("/DTransferRoot={0}" -f $TransferRoot) `
    $InstallerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE"
}

$SetupPath = Join-Path $Root ("Output\KillConfirmGameBar_Setup_{0}.exe" -f $Version)
if (-not (Test-Path $SetupPath)) {
    throw "Expected installer was not produced: $SetupPath"
}

Write-Host ""
Write-Host ("Installer: {0}" -f $SetupPath)
