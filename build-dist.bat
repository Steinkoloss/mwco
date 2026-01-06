@echo off
REM MWCO Build Script for Windows - Compiles server executable and mod package

setlocal enabledelayedexpansion

echo.
echo ======================================
echo   MWCO - My Winter Car Online
echo   Build Script (Windows)
echo ======================================
echo.

set BUILD_DIR=build
set DIST_DIR=dist

REM Clean previous builds
echo Cleaning previous builds...
if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
mkdir "%BUILD_DIR%"
mkdir "%DIST_DIR%"

REM Build the solution
echo Building MWCO solution...
dotnet build MWCO.slnx --configuration Release
if errorlevel 1 (
    echo Error: Build failed!
    exit /b 1
)

REM Create server distribution
echo.
echo Creating server distribution...
mkdir "%BUILD_DIR%\server"

REM Publish server as self-contained executable
echo Publishing server as standalone executable...
dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj ^
    --configuration Release ^
    --self-contained ^
    --runtime win-x64 ^
    --output "%BUILD_DIR%\server"

if errorlevel 1 (
    echo Error: Server publish failed!
    exit /b 1
)

REM Copy server executable to dist
if exist "%BUILD_DIR%\server\MWCO.Server.exe" (
    copy "%BUILD_DIR%\server\MWCO.Server.exe" "%DIST_DIR%\mwco-server.exe"
    echo Server executable created: dist\mwco-server.exe
) else (
    echo Error: Server executable not found!
    exit /b 1
)

REM Create mod package
echo.
echo Creating mod package...
mkdir "%BUILD_DIR%\mod\BepInEx\plugins"

REM Copy client DLLs
if exist "MWCO.Client\bin\Release\netstandard2.1\MWCO.Client.dll" (
    copy "MWCO.Client\bin\Release\netstandard2.1\MWCO.Client.dll" "%BUILD_DIR%\mod\BepInEx\plugins\"
    echo Copied MWCO.Client.dll
) else (
    echo Error: MWCO.Client.dll not found!
    exit /b 1
)

if exist "MWCO.Shared\bin\Release\netstandard2.1\MWCO.Shared.dll" (
    copy "MWCO.Shared\bin\Release\netstandard2.1\MWCO.Shared.dll" "%BUILD_DIR%\mod\BepInEx\plugins\"
    echo Copied MWCO.Shared.dll
) else (
    echo Error: MWCO.Shared.dll not found!
    exit /b 1
)

REM Copy Harmony dependency
if exist "MWCO.Client\bin\Release\netstandard2.1\0Harmony.dll" (
    copy "MWCO.Client\bin\Release\netstandard2.1\0Harmony.dll" "%BUILD_DIR%\mod\BepInEx\plugins\"
    echo Copied 0Harmony.dll
)

REM Create a README for the mod package
echo Creating INSTALL instructions...
(
    echo MWCO Mod Installation
    echo =====================
    echo.
    echo 1. Make sure you have BepInEx 5.x installed in your My Winter Car game directory.
    echo.
    echo 2. Extract the mwco-mod.zip to your game directory.
    echo    It will merge the BepInEx folder automatically.
    echo.
    echo 3. Start the MWCO server ^(mwco-server.exe^)
    echo.
    echo 4. Launch My Winter Car
    echo.
    echo 5. Press F10 in-game to open the MWCO menu
    echo.
    echo 6. Enter your server address and click Connect
    echo.
) > "%BUILD_DIR%\mod\INSTALL.txt"

REM Create ZIP archive (using Windows built-in if available, otherwise copy structure)
echo.
echo Creating mod ZIP package...
pushd "%BUILD_DIR%"
powershell -Command "Add-Type -AssemblyName 'System.IO.Compression.FileSystem'; [System.IO.Compression.ZipFile]::CreateFromDirectory('mod', '..\%DIST_DIR%\mwco-mod.zip')" 
popd

if errorlevel 1 (
    echo Warning: PowerShell ZIP failed, attempting alternative method...
)

REM Create installer script
echo Creating installer script...
(
    echo @echo off
    echo setlocal enabledelayedexpansion
    echo.
    echo echo ======================================
    echo echo   MWCO - My Winter Car Online
    echo echo   Mod Installer
    echo echo ======================================
    echo echo.
    echo.
    echo set GAME_DIR=%%APPDATA%%\Local\Temp\My Winter Car
    echo if not exist "%%GAME_DIR%%" (
    echo     echo Error: My Winter Car not found
    echo     echo.
    echo     echo Please check your game installation
    echo     pause
    echo     exit /b 1
    echo ^)
    echo.
    echo if not exist "%%GAME_DIR%%\BepInEx" (
    echo     echo Error: BepInEx not found!
    echo     echo.
    echo     echo Please install BepInEx 5.x first:
    echo     echo   1. Download from: https://github.com/BepInEx/BepInEx/releases
    echo     echo   2. Extract to your game directory
    echo     echo   3. Run the game once
    echo     echo   4. Run this installer again
    echo     pause
    echo     exit /b 1
    echo ^)
    echo.
    echo set SCRIPT_DIR=%%~dp0
    echo.
    echo echo Extracting mod files...
    echo powershell -Command "Add-Type -AssemblyName 'System.IO.Compression.FileSystem'; [System.IO.Compression.ZipFile]::ExtractToDirectory('%%SCRIPT_DIR%%mwco-mod.zip', '%%GAME_DIR%%')"
    echo.
    echo if errorlevel 1 (
    echo     echo Error: Extraction failed
    echo     pause
    echo     exit /b 1
    echo ^)
    echo.
    echo echo.
    echo echo ======================================
    echo echo   MWCO Mod Installed Successfully!
    echo echo ======================================
    echo echo.
    echo echo Next steps:
    echo echo   1. Run: mwco-server.exe
    echo echo   2. Launch My Winter Car
    echo echo   3. Press F10 to connect
    echo echo.
    echo pause
) > "%DIST_DIR%\install-mod.bat"

REM Summary
echo.
echo ======================================
echo   Build Complete!
echo ======================================
echo.
echo Distribution files created in 'dist\' directory:
echo.
echo 1. SERVER (Runnable^):
echo    dist\mwco-server.exe
echo    - Start the multiplayer server
echo    - Usage: mwco-server.exe [port]
echo    - Default port: 1999
echo.
echo 2. MOD PACKAGE (Install into game^):
echo    dist\mwco-mod.zip
echo    dist\install-mod.bat
echo    - Simple installer script to deploy mod
echo    - Usage: double-click install-mod.bat
echo.
echo Next steps:
echo   1. To run the server:
echo      mwco-server.exe
echo.
echo   2. To install the mod:
echo      install-mod.bat
echo.
pause
