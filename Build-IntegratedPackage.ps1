param(
    [string]$Configuration = "Debug",
    [string]$Platform = "x64",
    [string]$MsBuildPath = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$WorkspaceRoot = Split-Path -Parent $Root
$SourceAssetsCandidates = @(
    (Join-Path $Root "SourceAssets"),
    (Join-Path $WorkspaceRoot "SourceAssets"),
    (Join-Path $WorkspaceRoot "Big plan\SourceAssets")
)
$SourceAssetsRoot = $SourceAssetsCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $SourceAssetsRoot) {
    $SourceAssetsRoot = Join-Path $Root "SourceAssets"
}
$AnimationSourceRoot = Join-Path $SourceAssetsRoot "Animations"
$AudioSourceRoot = Join-Path $SourceAssetsRoot "Audio"
$SoundPackSourceRoot = Join-Path $SourceAssetsRoot "SoundPacks"
$ServiceRoot = Join-Path $Root "KillConfirmService"
$WidgetRoot = Join-Path $Root "Widget"
$PackageRoot = Join-Path $Root "Package"
$PackageProjectPath = Join-Path $PackageRoot "KillConfirmGameBar.Package.wapproj"
$WidgetKillAssetRoot = Join-Path $WidgetRoot "Assets\KillConfirm"
$PackagedServiceRoot = Join-Path $WidgetRoot "KillConfirmService"
$PackagedSoundsRoot = Join-Path $PackagedServiceRoot "sounds"
$ServiceSoundsRoot = Join-Path $ServiceRoot "sounds"
$FrameSequenceAssets = @(
    @{ Source = Join-Path $AnimationSourceRoot "1kill-remasterd\1kill"; Target = "1killre" },
    @{ Source = Join-Path $AnimationSourceRoot "2kill-remasterd\2kill"; Target = "2killre" },
    @{ Source = Join-Path $AnimationSourceRoot "3killre\3killre"; Target = "3killre" },
    @{ Source = Join-Path $AnimationSourceRoot "4killre\4killre"; Target = "4killre" },
    @{ Source = Join-Path $AnimationSourceRoot "5killre\5killre"; Target = "5killre" },
    @{ Source = Join-Path $AnimationSourceRoot "6killre\6killre"; Target = "6killre" },
    @{ Source = Join-Path $AnimationSourceRoot "Firstkill\Firstkill"; Target = "firstkill" },
    @{ Source = Join-Path $AnimationSourceRoot "Headshot_silver\Headshot_silver"; Target = "headshot_silver" },
    @{ Source = Join-Path $AnimationSourceRoot "GoldHeadshot\goldheadshot"; Target = "goldheadshot" },
    @{ Source = Join-Path $AnimationSourceRoot "knife_kill\knife_kill"; Target = "knife_kill" },
    @{ Source = Join-Path $AnimationSourceRoot "last_kill\last_kill"; Target = "last_kill" }
)
$ObsoleteFrameSequenceTargets = @(
    "headshot"
)

if (-not (Get-Command cargo -ErrorAction SilentlyContinue)) {
    $UserCargo = Join-Path $env:USERPROFILE ".cargo\bin\cargo.exe"
    if (Test-Path $UserCargo) {
        $CargoPath = $UserCargo
    }
    else {
        throw "cargo was not found. Install Rust first, then run this script again."
    }
}
else {
    $CargoPath = (Get-Command cargo).Source
}

if (-not $MsBuildPath) {
    $MsBuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($MsBuildCommand) {
        $MsBuildPath = $MsBuildCommand.Source
    }
    else {
        $MsBuildCandidates = @(
            "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
            "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
            "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        )
        $MsBuildPath = $MsBuildCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}

if (-not $MsBuildPath -or -not (Test-Path $MsBuildPath)) {
    throw "MSBuild was not found. Install Visual Studio Build Tools or pass -MsBuildPath."
}

if (-not (Test-Path $PackageProjectPath)) {
    throw "Packaging project was not found at $PackageProjectPath"
}

if (-not (Test-Path $SoundPackSourceRoot)) {
    throw "Sound pack source folder was not found at $SoundPackSourceRoot"
}

New-Item -ItemType Directory -Force -Path $WidgetKillAssetRoot | Out-Null
for ($spriteNumber = 1; $spriteNumber -le 5; $spriteNumber++) {
    foreach ($extension in @("png", "json")) {
        $obsoleteSpriteSheet = Join-Path $WidgetKillAssetRoot ("{0}.{1}" -f $spriteNumber, $extension)
        if (Test-Path $obsoleteSpriteSheet) {
            Remove-Item -LiteralPath $obsoleteSpriteSheet -Force
        }
    }
}

$resolvedServiceRootForSounds = [System.IO.Path]::GetFullPath($ServiceRoot)
$resolvedServiceSoundsRoot = [System.IO.Path]::GetFullPath($ServiceSoundsRoot)
if (-not $resolvedServiceSoundsRoot.StartsWith($resolvedServiceRootForSounds, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an unexpected service sounds path: $resolvedServiceSoundsRoot"
}

if (Test-Path $ServiceSoundsRoot) {
    Remove-Item -LiteralPath $ServiceSoundsRoot -Recurse -Force
}
Copy-Item -LiteralPath $SoundPackSourceRoot -Destination $ServiceSoundsRoot -Recurse -Force

$CrossfireSoundRoot = Join-Path $ServiceSoundsRoot "crossfire"
New-Item -ItemType Directory -Force -Path $CrossfireSoundRoot | Out-Null
$SharedCrossfireAudioAssets = @(
    @{ Source = Join-Path $AudioSourceRoot "Knifekill_CT.wav"; Target = "knife.wav" },
    @{ Source = Join-Path $AudioSourceRoot "firstandlast_CT.wav"; Target = "firstandlast.wav" }
)
foreach ($asset in $SharedCrossfireAudioAssets) {
    if (Test-Path $asset.Source) {
        Copy-Item -LiteralPath $asset.Source -Destination (Join-Path $CrossfireSoundRoot $asset.Target) -Force
    }
}

$CrossfireSharedFiles = @("sound.lua", "common.wav", "knife.wav", "firstandlast.wav")
$CrossfireVariantRoots = @(
    (Join-Path $ServiceSoundsRoot "crossfire_v_fhd"),
    (Join-Path $ServiceSoundsRoot "crossfire_v_sex")
)
foreach ($variantRoot in $CrossfireVariantRoots) {
    if (-not (Test-Path $variantRoot)) {
        continue
    }

    foreach ($fileName in $CrossfireSharedFiles) {
        $sourceFile = Join-Path $CrossfireSoundRoot $fileName
        if (Test-Path $sourceFile) {
            Copy-Item -LiteralPath $sourceFile -Destination (Join-Path $variantRoot $fileName) -Force
        }
    }
}

foreach ($asset in $FrameSequenceAssets) {
    $targetAssetRoot = Join-Path $WidgetKillAssetRoot $asset.Target
    $resolvedWidgetKillAssetRoot = [System.IO.Path]::GetFullPath($WidgetKillAssetRoot)
    $resolvedTargetAssetRoot = [System.IO.Path]::GetFullPath($targetAssetRoot)
    if (-not $resolvedTargetAssetRoot.StartsWith($resolvedWidgetKillAssetRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected animation asset path: $resolvedTargetAssetRoot"
    }

    if (Test-Path $targetAssetRoot) {
        Remove-Item -LiteralPath $targetAssetRoot -Recurse -Force
    }

    if (Test-Path $asset.Source) {
        New-Item -ItemType Directory -Force -Path $targetAssetRoot | Out-Null
        Get-ChildItem -LiteralPath $asset.Source -File | Sort-Object Name | Copy-Item -Destination $targetAssetRoot -Force
    }
}

foreach ($targetName in $ObsoleteFrameSequenceTargets) {
    $targetAssetRoot = Join-Path $WidgetKillAssetRoot $targetName
    $resolvedWidgetKillAssetRoot = [System.IO.Path]::GetFullPath($WidgetKillAssetRoot)
    $resolvedTargetAssetRoot = [System.IO.Path]::GetFullPath($targetAssetRoot)
    if (-not $resolvedTargetAssetRoot.StartsWith($resolvedWidgetKillAssetRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean an unexpected obsolete animation asset path: $resolvedTargetAssetRoot"
    }

    if (Test-Path $targetAssetRoot) {
        Remove-Item -LiteralPath $targetAssetRoot -Recurse -Force
    }
}

$VsWhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $VsWhere)) {
    throw "vswhere was not found. Install Visual Studio or Visual Studio Build Tools with the C++ toolchain."
}

$VcInstallPath = & $VsWhere -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if (-not $VcInstallPath) {
    throw "Visual C++ x64 build tools were not found. Install the 'Desktop development with C++' workload or 'Visual Studio Build Tools' with the VCTools workload."
}

$VsDevCmd = Join-Path $VcInstallPath "Common7\Tools\VsDevCmd.bat"
if (-not (Test-Path $VsDevCmd)) {
    throw "VsDevCmd.bat was not found under $VcInstallPath"
}

Push-Location $ServiceRoot
try {
    $CargoCommand = '"' + $VsDevCmd + '" -arch=x64 -host_arch=x64 && "' + $CargoPath + '" build --release'
    & $env:ComSpec /c $CargoCommand
    if ($LASTEXITCODE -ne 0) {
        throw "cargo build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

New-Item -ItemType Directory -Force -Path $PackagedServiceRoot | Out-Null

$ServiceExe = Join-Path $ServiceRoot "target\release\cskillconfirm.exe"
if (-not (Test-Path $ServiceExe)) {
    throw "Expected service executable was not produced: $ServiceExe"
}

Copy-Item -LiteralPath $ServiceExe -Destination (Join-Path $PackagedServiceRoot "cskillconfirm.exe") -Force

$resolvedServiceRoot = [System.IO.Path]::GetFullPath($PackagedServiceRoot)
$resolvedSoundsRoot = [System.IO.Path]::GetFullPath($PackagedSoundsRoot)
if (-not $resolvedSoundsRoot.StartsWith($resolvedServiceRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean an unexpected sounds path: $resolvedSoundsRoot"
}

if (Test-Path $PackagedSoundsRoot) {
    Remove-Item -LiteralPath $PackagedSoundsRoot -Recurse -Force
}

Copy-Item -LiteralPath (Join-Path $ServiceRoot "sounds") -Destination $PackagedSoundsRoot -Recurse -Force

Push-Location $PackageRoot
try {
    $PackageOutputFolder = "Integrated_{0}_Package" -f $Configuration
    & $MsBuildPath KillConfirmGameBar.Package.wapproj /p:Configuration=$Configuration /p:Platform=$Platform /p:AppxPackageDir=AppPackages\$PackageOutputFolder\ /t:Rebuild /verbosity:minimal
}
finally {
    Pop-Location
}
