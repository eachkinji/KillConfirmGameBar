param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$ManifestPath = Join-Path $Root "Package\Package.appxmanifest"

if (-not (Test-Path $ManifestPath)) {
    throw "Package.appxmanifest was not found at $ManifestPath"
}

[xml]$Manifest = Get-Content $ManifestPath
$Version = $Manifest.Package.Identity.Version
if (-not $Version) {
    throw "Could not read package version from $ManifestPath"
}

$PackageFolderName = "KillConfirmGameBar.Package_{0}_{1}_{2}_Test" -f $Version, $Platform, $Configuration
$PackageFileName = "KillConfirmGameBar.Package_{0}_{1}_{2}.msix" -f $Version, $Platform, $Configuration
$PackageSourceRoot = Join-Path $Root ("Package\AppPackages\Integrated_Debug_Test\{0}" -f $PackageFolderName)
$AppPackagesRoot = Join-Path $Root "Package\AppPackages"
$TransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}" -f $Version)
$TransferZip = "{0}.zip" -f $TransferRoot
$ExpectedPackageFamilyName = "KillConfirmGameBar.Overlay_5jgcw66eyez0m"

$resolvedWorkspaceRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)
$resolvedTransferRoot = [System.IO.Path]::GetFullPath($TransferRoot)
if (-not $resolvedTransferRoot.StartsWith($resolvedWorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the workspace root: $resolvedTransferRoot"
}

Get-Process cskillconfirm -ErrorAction SilentlyContinue | Stop-Process -Force

& (Join-Path $Root "Build-IntegratedPackage.ps1") -Configuration $Configuration -Platform $Platform -MsBuildPath $MsBuildPath

if (-not (Test-Path $PackageSourceRoot)) {
    throw "Expected package folder was not produced: $PackageSourceRoot"
}

if (-not (Test-Path (Join-Path $PackageSourceRoot $PackageFileName))) {
    throw "Expected package file was not produced: $(Join-Path $PackageSourceRoot $PackageFileName)"
}

if (Test-Path $TransferRoot) {
    Remove-Item -LiteralPath $TransferRoot -Recurse -Force
}

if (Test-Path $TransferZip) {
    Remove-Item -LiteralPath $TransferZip -Force
}

$OverlayTransferRoot = Join-Path $TransferRoot "OverlayPackage"

New-Item -ItemType Directory -Force -Path $OverlayTransferRoot | Out-Null

Copy-Item -Path (Join-Path $PackageSourceRoot "*") -Destination $OverlayTransferRoot -Recurse -Force

$InstallScript = @'
param(
    [switch]$SkipLoopback = $false,
    [switch]$SkipGsiConfig = $false,
    [switch]$OpenGameBar = $false
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OverlayRoot = Join-Path $ScriptRoot "OverlayPackage"
$InstallEntry = Join-Path $OverlayRoot "Install.ps1"
$PackageFamilyName = "KillConfirmGameBar.Overlay_5jgcw66eyez0m"

if (-not (Test-Path $InstallEntry)) {
    throw "Install.ps1 was not found under $OverlayRoot"
}

& powershell -ExecutionPolicy Bypass -File $InstallEntry -Force

function Get-SteamLibraryRoots {
    $roots = New-Object System.Collections.Generic.List[string]

    $registryPaths = @(
        "HKCU:\Software\Valve\Steam",
        "HKLM:\Software\WOW6432Node\Valve\Steam",
        "HKLM:\Software\Valve\Steam"
    )

    foreach ($registryPath in $registryPaths) {
        try {
            $steam = Get-ItemProperty -Path $registryPath -ErrorAction Stop
            foreach ($property in @("SteamPath", "InstallPath")) {
                if ($steam.$property) {
                    $roots.Add(($steam.$property -replace "/", "\"))
                }
            }
        }
        catch {
        }
    }

    $programFilesX86 = ${env:ProgramFiles(x86)}
    if ($programFilesX86) {
        $roots.Add((Join-Path $programFilesX86 "Steam"))
    }

    $allRoots = New-Object System.Collections.Generic.List[string]
    foreach ($root in $roots) {
        if (-not $root -or -not (Test-Path $root)) {
            continue
        }

        $fullRoot = [System.IO.Path]::GetFullPath($root)
        if (-not $allRoots.Contains($fullRoot)) {
            $allRoots.Add($fullRoot)
        }

        $libraryFolders = Join-Path $fullRoot "steamapps\libraryfolders.vdf"
        if (-not (Test-Path $libraryFolders)) {
            continue
        }

        foreach ($line in Get-Content -LiteralPath $libraryFolders -ErrorAction SilentlyContinue) {
            if ($line -match '^\s*"\d+"\s+"([^"]+)"') {
                $path = $matches[1] -replace "\\\\", "\"
                if (Test-Path $path) {
                    $fullPath = [System.IO.Path]::GetFullPath($path)
                    if (-not $allRoots.Contains($fullPath)) {
                        $allRoots.Add($fullPath)
                    }
                }
            }
            elseif ($line -match '^\s*"path"\s+"([^"]+)"') {
                $path = $matches[1] -replace "\\\\", "\"
                if (Test-Path $path) {
                    $fullPath = [System.IO.Path]::GetFullPath($path)
                    if (-not $allRoots.Contains($fullPath)) {
                        $allRoots.Add($fullPath)
                    }
                }
            }
        }
    }

    return $allRoots
}

function Install-Cs2GsiConfig {
    $configText = @"
"KillConfirmGameBar"
{
 "uri" "http://127.0.0.1:3000/"
 "timeout" "5.0"
 "buffer"  "0.1"
 "throttle" "0.1"
 "heartbeat" "30.0"
 "data"
 {
   "provider"           "1"
   "map"                "1"
   "round"              "1"
   "player_id"          "1"
   "player_state"       "1"
   "player_weapons"     "1"
   "player_match_stats" "1"
 }
}
"@

    $installed = $false
    foreach ($libraryRoot in Get-SteamLibraryRoots) {
        $cfgRoot = Join-Path $libraryRoot "steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg"
        if (-not (Test-Path $cfgRoot)) {
            continue
        }

        $cfgPath = Join-Path $cfgRoot "gamestate_integration_killconfirm.cfg"
        Set-Content -LiteralPath $cfgPath -Value $configText -Encoding ASCII
        Write-Host "CS2 GSI config installed: $cfgPath"
        $installed = $true
    }

    if (-not $installed) {
        Write-Warning "CS2 cfg folder was not found. Install gamestate_integration_killconfirm.cfg manually if kill events do not trigger."
    }
}

if (-not $SkipGsiConfig) {
    Install-Cs2GsiConfig
}

if (-not $SkipLoopback) {
    & CheckNetIsolation LoopbackExempt -a -n=$PackageFamilyName
}

if ($OpenGameBar) {
    Start-Sleep -Milliseconds 800
    try {
        Start-Process "ms-gamebar:" | Out-Null
    }
    catch {
    }
}

Write-Host ""
Write-Host "Kill Confirm installed."
Write-Host "Press Win+G and launch Kill Confirm Overlay."
Write-Host "The packaged background service starts from inside the app."
'@

$Readme = @'
KillConfirmGameBar transfer package

What is inside:
- OverlayPackage: the Xbox Game Bar MSIX package and its dependencies
- Install-KillConfirm.ps1: one-click install script

Use on another PC:
1. Right-click Install-KillConfirm.ps1 and run with PowerShell
2. Press Win+G
3. Launch Kill Confirm Overlay from Xbox Game Bar
4. Use the panel power button or Check button if you want to verify status

Notes:
- The companion service is embedded inside the MSIX package.
- The widget starts its packaged companion service directly from the installed app.
- The install script tries to create CS2's gamestate_integration_killconfirm.cfg automatically.
- The widget still talks to 127.0.0.1 internally, so the install script adds the required loopback exemption.
- If Xbox Game Bar was open during install, close it and open it again.
- The installer does not auto-open the widget URI because some Windows installs do not register ms-gamebarwidget links.
- Package family name for loopback: KillConfirmGameBar.Overlay_5jgcw66eyez0m
'@

Set-Content -LiteralPath (Join-Path $TransferRoot "Install-KillConfirm.ps1") -Value $InstallScript -Encoding UTF8
Set-Content -LiteralPath (Join-Path $TransferRoot "README.txt") -Value $Readme -Encoding UTF8

Compress-Archive -Path (Join-Path $TransferRoot "*") -DestinationPath $TransferZip -Force

$resolvedAppPackagesRoot = [System.IO.Path]::GetFullPath($AppPackagesRoot)
if ($resolvedAppPackagesRoot.StartsWith($resolvedWorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path $AppPackagesRoot)) {
    Get-ChildItem -LiteralPath $AppPackagesRoot -Force | ForEach-Object {
        Remove-Item -LiteralPath $_.FullName -Recurse -Force
    }
}

Write-Host ""
Write-Host ("Transfer folder: {0}" -f $TransferRoot)
Write-Host ("Transfer zip:    {0}" -f $TransferZip)
Write-Host ("MSIX package:    {0}" -f (Join-Path $OverlayTransferRoot $PackageFileName))
Write-Host ("Package family:  {0}" -f $ExpectedPackageFamilyName)
