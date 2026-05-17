@echo off
echo [1/3] Killing existing processes...
powershell -Command "taskkill /F /IM cskillconfirm.exe /T; taskkill /F /IM TestXboxGameBar.exe /T; exit 0"

echo [2/3] Building the updated package (Release)...
powershell -ExecutionPolicy Bypass -File "Build-IntegratedPackage.ps1" -Configuration Release

echo [3/3] Locating the new package...
for /f %%V in ('powershell -NoProfile -Command "[xml]$m=Get-Content ''Package\Package.appxmanifest''; $m.Package.Identity.Version"') do set VERSION=%%V
set PACKAGE_DIR=Package\AppPackages\Integrated_Release_Package\KillConfirmGameBar.Package_%VERSION%_x64_Test
if exist "%PACKAGE_DIR%\Add-AppDevPackage.ps1" (
    echo [SUCCESS] Found version %VERSION%.
    echo Please run this command manually to finish:
    echo powershell -ExecutionPolicy Bypass -File "%PACKAGE_DIR%\Add-AppDevPackage.ps1"
) else (
    echo [ERROR] Could not find the package at %PACKAGE_DIR%.
    echo Please check the build output above.
)
pause
