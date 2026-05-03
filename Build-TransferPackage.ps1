param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$MsBuildPath = "",
    [switch]$NoProcessShutdown
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
$PackageOutputFolder = "Integrated_{0}_Package" -f $Configuration
$PackageSourceRoot = Join-Path $Root ("Package\AppPackages\{0}\{1}" -f $PackageOutputFolder, $PackageFolderName)
$AppPackagesRoot = Join-Path $Root "Package\AppPackages"
$TransferRoot = Join-Path $WorkspaceRoot ("KillConfirmGameBar_Transfer_{0}" -f $Version)
$TransferZip = "{0}.zip" -f $TransferRoot
$ExpectedPackageFamilyName = "KillConfirmGameBar.Overlay_5jgcw66eyez0m"

$resolvedWorkspaceRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)
$resolvedTransferRoot = [System.IO.Path]::GetFullPath($TransferRoot)
if (-not $resolvedTransferRoot.StartsWith($resolvedWorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the workspace root: $resolvedTransferRoot"
}

$KillConfirmProcessNames = @(
    "cskillconfirm",
    "TestXboxGameBar",
    "KillConfirmOverlay",
    "KillConfirmGameBar",
    "GameBar",
    "GameBarFTServer",
    "GameBarPresenceWriter"
)

Get-Process -Name $KillConfirmProcessNames -ErrorAction SilentlyContinue | Stop-Process -Force

& (Join-Path $Root "Build-IntegratedPackage.ps1") -Configuration $Configuration -Platform $Platform -MsBuildPath $MsBuildPath

if (-not (Test-Path (Join-Path $PackageSourceRoot $PackageFileName))) {
    $ProducedPackage = Get-ChildItem -LiteralPath $AppPackagesRoot -Filter "*.msix" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "*$Version*" -and $_.Name -like "*$Platform*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($ProducedPackage) {
        $PackageSourceRoot = $ProducedPackage.DirectoryName
        $PackageFileName = $ProducedPackage.Name
    }
}

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

Copy-Item -LiteralPath (Join-Path $PackageSourceRoot $PackageFileName) -Destination $OverlayTransferRoot -Force

$PackageCertificate = Get-ChildItem -LiteralPath $PackageSourceRoot -Filter "*.cer" -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($PackageCertificate) {
    Copy-Item -LiteralPath $PackageCertificate.FullName -Destination $OverlayTransferRoot -Force
}

$DependencySourceRoot = Join-Path $PackageSourceRoot "Dependencies\$Platform"
if (Test-Path $DependencySourceRoot) {
    $DependencyTargetRoot = Join-Path $OverlayTransferRoot "Dependencies\$Platform"
    New-Item -ItemType Directory -Force -Path $DependencyTargetRoot | Out-Null
    Get-ChildItem -LiteralPath $DependencySourceRoot -Include "*.appx", "*.msix" -File -Recurse |
        Copy-Item -Destination $DependencyTargetRoot -Force
}

$InstallScript = @'
param(
    [switch]$SkipLoopback = $false,
    [switch]$SkipGsiConfig = $false,
    [switch]$OpenGameBar = $false,
    [bool]$NoProcessShutdown = __NO_PROCESS_SHUTDOWN__
)

$ErrorActionPreference = "Stop"

$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$OverlayRoot = Join-Path $ScriptRoot "OverlayPackage"
$PackageFamilyName = "KillConfirmGameBar.Overlay_5jgcw66eyez0m"
$LogPath = Join-Path $env:TEMP "KillConfirmGameBar_Install.log"

function Write-InstallLog {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $Message
}

function Get-AppxIdentityFromPackageFile {
    param([string]$PackagePath)

    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
        $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
        try {
            $entry = $zip.GetEntry("AppxManifest.xml")
            if (-not $entry) {
                return $null
            }

            $reader = New-Object System.IO.StreamReader($entry.Open())
            try {
                [xml]$manifest = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            if (-not $manifest.Package.Identity.Name) {
                return $null
            }

            return [pscustomobject]@{
                Name = $manifest.Package.Identity.Name
                Version = [version]$manifest.Package.Identity.Version
                Publisher = $manifest.Package.Identity.Publisher
            }
        }
        finally {
            $zip.Dispose()
        }
    }
    catch {
        Write-InstallLog "Could not inspect package identity for ${PackagePath}: $($_.Exception.Message)"
        return $null
    }
}

function Test-AppxPackageInstalled {
    param([string]$PackagePath)

    $identity = Get-AppxIdentityFromPackageFile -PackagePath $PackagePath
    if (-not $identity) {
        return $false
    }

    $installed = Get-AppxPackage -Name $identity.Name -ErrorAction SilentlyContinue |
        Sort-Object Version -Descending |
        Select-Object -First 1
    if (-not $installed) {
        return $false
    }

    try {
        return ([version]$installed.Version -ge $identity.Version)
    }
    catch {
        return $true
    }
}

function Write-AppxFailureDetails {
    param([System.Management.Automation.ErrorRecord]$ErrorRecord)

    Write-InstallLog ("Install failed: {0}" -f $ErrorRecord.Exception.Message)
    $details = ($ErrorRecord | Format-List * -Force | Out-String)
    Add-Content -LiteralPath $LogPath -Value $details -Encoding UTF8

    $activityId = $null
    if ($ErrorRecord.Exception -and $ErrorRecord.Exception.ActivityId) {
        $activityId = $ErrorRecord.Exception.ActivityId
    }

    if ($activityId) {
        try {
            Write-InstallLog "AppX deployment activity id: $activityId"
            $activityLog = Get-AppPackageLog -ActivityID $activityId -ErrorAction Stop | Out-String
            Add-Content -LiteralPath $LogPath -Value $activityLog -Encoding UTF8
        }
        catch {
            Write-InstallLog "Could not read AppX activity log: $($_.Exception.Message)"
        }
    }

    try {
        $events = Get-WinEvent -LogName "Microsoft-Windows-AppXDeploymentServer/Operational" -MaxEvents 30 -ErrorAction Stop |
            Select-Object TimeCreated, Id, LevelDisplayName, ProviderName, Message |
            Format-List |
            Out-String
        Add-Content -LiteralPath $LogPath -Value $events -Encoding UTF8
    }
    catch {
        Write-InstallLog "Could not read AppX deployment event log: $($_.Exception.Message)"
    }
}

function Add-AppxPackageCompat {
    param(
        [string]$PackagePath,
        [switch]$ForceUpdate,
        [switch]$DeferWhenInUse
    )

    $command = Get-Command Add-AppxPackage -ErrorAction Stop
    $addPackageParams = @{
        Path = $PackagePath
        ErrorAction = "Stop"
    }

    if ($ForceUpdate -and $command.Parameters.ContainsKey("ForceUpdateFromAnyVersion")) {
        $addPackageParams.ForceUpdateFromAnyVersion = $true
    }
    if ($DeferWhenInUse -and $command.Parameters.ContainsKey("DeferRegistrationWhenPackagesAreInUse")) {
        $addPackageParams.DeferRegistrationWhenPackagesAreInUse = $true
    }
    try {
        Add-AppxPackage @addPackageParams
    }
    catch {
        Write-AppxFailureDetails -ErrorRecord $_
        throw
    }
}

function Install-OverlayPackage {
    if (-not (Test-Path $OverlayRoot)) {
        throw "OverlayPackage was not found under $ScriptRoot"
    }

    if (-not $NoProcessShutdown) {
        $processNames = @(
            "cskillconfirm",
            "TestXboxGameBar",
            "KillConfirmOverlay",
            "KillConfirmGameBar",
            "GameBar",
            "GameBarFTServer",
            "GameBarPresenceWriter"
        )
        Get-Process -Name $processNames -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Milliseconds 800
    }
    else {
        Write-InstallLog "Skipping process shutdown before MSIX install."
    }

    $msix = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.msix" -File | Select-Object -First 1
    if (-not $msix) {
        throw "MSIX package was not found under $OverlayRoot"
    }

    $cert = Get-ChildItem -LiteralPath $OverlayRoot -Filter "*.cer" -File | Select-Object -First 1
    if ($cert) {
        Write-InstallLog "Installing package certificate..."
        Import-Certificate -FilePath $cert.FullName -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null
        Import-Certificate -FilePath $cert.FullName -CertStoreLocation "Cert:\LocalMachine\Root" | Out-Null
    }
    else {
        Write-InstallLog "No package certificate found beside MSIX."
    }

    $dependencies = @()
    $dependencyRoot = Join-Path $OverlayRoot "Dependencies\x64"
    if (Test-Path $dependencyRoot) {
        $dependencies = @(Get-ChildItem -LiteralPath $dependencyRoot -Include "*.appx", "*.msix" -File -Recurse | ForEach-Object { $_.FullName })
    }

    foreach ($dependency in $dependencies) {
        $dependencyName = Split-Path -Leaf $dependency
        if (Test-AppxPackageInstalled -PackagePath $dependency) {
            Write-InstallLog "Dependency already installed: $dependencyName"
            continue
        }

        Write-InstallLog "Installing dependency: $dependencyName"
        Add-AppxPackageCompat -PackagePath $dependency
    }

    Write-InstallLog "Installing MSIX package: $($msix.Name)"
    Add-AppxPackageCompat -PackagePath $msix.FullName -ForceUpdate -DeferWhenInUse
}

function Test-OverlayPackageInstalled {
    $package = Get-AppxPackage -Name "KillConfirmGameBar.Overlay" -ErrorAction SilentlyContinue
    if (-not $package) {
        throw "MSIX install finished, but KillConfirmGameBar.Overlay is not registered for this user."
    }

    Write-InstallLog "MSIX package registered: $($package.PackageFamilyName)"
}

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
    $configLines = @(
        '"KillConfirmGameBar"',
        '{',
        ' "uri" "http://127.0.0.1:3000/"',
        ' "timeout" "5.0"',
        ' "buffer"  "0.1"',
        ' "throttle" "0.1"',
        ' "heartbeat" "30.0"',
        ' "auth"',
        ' {',
        '   "token" "killconfirm"',
        ' }',
        ' "data"',
        ' {',
        '   "provider"           "1"',
        '   "map"                "1"',
        '   "round"              "1"',
        '   "player_id"          "1"',
        '   "player_state"       "1"',
        '   "player_weapons"     "1"',
        '   "player_match_stats" "1"',
        ' }',
        '}'
    )

    $installed = $false
    foreach ($libraryRoot in Get-SteamLibraryRoots) {
        $cfgRoot = Join-Path $libraryRoot "steamapps\common\Counter-Strike Global Offensive\game\csgo\cfg"
        if (-not (Test-Path $cfgRoot)) {
            continue
        }

        $cfgPath = Join-Path $cfgRoot "gamestate_integration_killconfirm.cfg"
        Set-Content -LiteralPath $cfgPath -Value $configLines -Encoding ASCII
        Write-Host "CS2 GSI config installed: $cfgPath"
        $installed = $true
    }

    if (-not $installed) {
        Write-Warning "CS2 cfg folder was not found. If kill events do not trigger, install gamestate_integration_killconfirm.cfg manually."
    }
}

try {
    if (Test-Path $LogPath) {
        Remove-Item -LiteralPath $LogPath -Force
    }

    Install-OverlayPackage
    Test-OverlayPackageInstalled

    if (-not $SkipGsiConfig) {
        Install-Cs2GsiConfig
    }

    if (-not $SkipLoopback) {
        Write-InstallLog "Adding loopback exemption for $PackageFamilyName..."
        & CheckNetIsolation.exe LoopbackExempt -a "-n=$PackageFamilyName"
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to add loopback exemption for $PackageFamilyName"
        }
    }

    if ($OpenGameBar) {
        Start-Sleep -Milliseconds 800
        try {
            Start-Process "ms-gamebar:" | Out-Null
        }
        catch {
        }
    }

    Write-InstallLog "Kill Confirm installed."
    Write-Host ""
    Write-Host "Kill Confirm installed."
    Write-Host "Press Win+G and launch Kill Confirm Overlay."
    Write-Host "The packaged background service starts from inside the app."
}
catch {
    Write-InstallLog "Install failed: $($_.Exception.Message)"
    Write-InstallLog "Log: $LogPath"
    throw
}
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
- The install script installs the MSIX package directly instead of requiring Visual Studio developer scripts.
- The install script tries to create CS2's gamestate_integration_killconfirm.cfg automatically.
- The widget talks to 127.0.0.1 internally, so the install script adds the required loopback exemption.
- If Xbox Game Bar was open during install, close it and open it again.
- The installer does not auto-open the widget URI because some Windows installs do not register ms-gamebarwidget links.
- Package family name for loopback: KillConfirmGameBar.Overlay_5jgcw66eyez0m

KillConfirmGameBar transfer package - Chinese quick guide

1. Right-click Install-KillConfirm.ps1 and choose Run with PowerShell.
2. Press Win+G.
3. Open Kill Confirm Overlay in Xbox Game Bar.
4. If the status is not green, use the panel Check button and the CFG check area.
'@

$InstallScript = $InstallScript.Replace(
    "__NO_PROCESS_SHUTDOWN__",
    $(if ($NoProcessShutdown) { '$true' } else { '$false' })
)

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
