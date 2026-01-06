#!/usr/bin/env python3
"""
MWCO Build & Distribution Script
Automates building and packaging MWCO server and mod
Works on Windows and Linux
"""

import os
import sys
import subprocess
import shutil
import platform
import zipfile
from pathlib import Path

class Colors:
    GREEN = '\033[0;32m'
    RED = '\033[0;31m'
    YELLOW = '\033[1;33m'
    NC = '\033[0m'

def is_windows():
    return platform.system() == "Windows"

def print_header():
    print(f"{Colors.GREEN}{'='*50}")
    print("  MWCO - My Winter Car Online")
    print("  Build & Distribution System")
    print(f"{'='*50}{Colors.NC}\n")

def print_success(msg):
    print(f"{Colors.GREEN}✓ {msg}{Colors.NC}")

def print_error(msg):
    print(f"{Colors.RED}✗ {msg}{Colors.NC}")

def print_info(msg):
    print(f"{Colors.YELLOW}→ {msg}{Colors.NC}")

def run_command(cmd, description):
    """Run a shell command and return success status"""
    print_info(description)
    try:
        result = subprocess.run(cmd, shell=True, cwd=os.getcwd(), capture_output=True, text=True)
        if result.returncode != 0:
            print_error(f"Failed: {description}")
            if result.stderr:
                print(f"  Error: {result.stderr[:200]}")
            return False
        print_success(description)
        return True
    except Exception as e:
        print_error(f"Exception: {e}")
        return False

def create_zip(source_dir, zip_path):
    """Create a ZIP file from a directory"""
    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zipf:
        for root, dirs, files in os.walk(source_dir):
            for file in files:
                file_path = os.path.join(root, file)
                arcname = os.path.relpath(file_path, source_dir)
                zipf.write(file_path, arcname)

def main():
    print_header()
    
    # Verify we're in the right directory
    if not os.path.exists("MWCO.slnx"):
        print_error("Not in MWCO root directory!")
        sys.exit(1)
    
    # Detect platform
    platform_name = platform.system()
    runtime_id = "win-x64" if is_windows() else "linux-x64"
    exe_ext = ".exe" if is_windows() else ""
    
    print_info(f"Detected platform: {platform_name}")
    print_info(f"Using runtime: {runtime_id}\n")
    
    # Setup
    BUILD_DIR = "build"
    DIST_DIR = "dist"
    
    # Clean directories
    print_info("Preparing build directories...")
    if os.path.exists(BUILD_DIR):
        shutil.rmtree(BUILD_DIR)
    if os.path.exists(DIST_DIR):
        shutil.rmtree(DIST_DIR)
    os.makedirs(BUILD_DIR, exist_ok=True)
    os.makedirs(DIST_DIR, exist_ok=True)
    print_success("Directories prepared\n")
    
    # Step 1: Build solution
    if not run_command("dotnet build MWCO.slnx --configuration Release -v minimal", 
                       "Building MWCO solution (Release)"):
        sys.exit(1)
    
    # Step 2: Publish server
    print()
    print_info("Creating server distribution...")
    server_build_dir = os.path.join(BUILD_DIR, "server")
    os.makedirs(server_build_dir, exist_ok=True)
    
    if not run_command(
        f"dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj "
        f"--configuration Release --self-contained --runtime {runtime_id} "
        f"--output {server_build_dir} -v minimal",
        "Publishing server executable"):
        sys.exit(1)
    
    # Copy server binary
    server_src = os.path.join(server_build_dir, f"MWCO.Server{exe_ext}")
    server_dst = os.path.join(DIST_DIR, f"mwco-server{exe_ext}")
    if os.path.exists(server_src):
        shutil.copy2(server_src, server_dst)
        if not is_windows():
            os.chmod(server_dst, 0o755)
        size = os.path.getsize(server_dst) / (1024*1024)
        print_success(f"Server executable created ({size:.1f} MB)")
    else:
        print_error("Server executable not found!")
        sys.exit(1)
    
    # Step 3: Create mod package
    print()
    print_info("Creating mod package...")
    mod_plugins_dir = os.path.join(BUILD_DIR, "mod", "BepInEx", "plugins")
    os.makedirs(mod_plugins_dir, exist_ok=True)
    
    # Copy DLLs
    dlls = [
        ("MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll", "MWCO.Client.dll"),
        ("MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll", "MWCO.Shared.dll"),
        ("MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll", "0Harmony.dll"),
    ]
    
    for src_dll, name in dlls:
        if os.path.exists(src_dll):
            shutil.copy2(src_dll, os.path.join(mod_plugins_dir, name))
            print_success(f"Copied {name}")
        else:
            print_error(f"Not found: {name}")
            sys.exit(1)
    
    # Create INSTALL instructions
    install_txt = os.path.join(BUILD_DIR, "mod", "INSTALL.txt")
    with open(install_txt, "w") as f:
        f.write("""MWCO Mod Installation
=====================

1. Make sure you have BepInEx 5.x installed in your My Winter Car directory:
   - Download from: https://github.com/BepInEx/BepInEx/releases
   - Extract to your game directory
   - Run the game once to initialize

2. Extract the mwco-mod.zip to your My Winter Car game directory
   It will merge the BepInEx folder automatically

3. Start the MWCO server (mwco-server.exe)

4. Launch My Winter Car

5. Press F10 in-game to open the MWCO connection menu

6. Enter your server address (default: 127.0.0.1:1999) and click Connect

Done! You're now playing My Winter Car Online!
""")
    
    # Create ZIP archive
    print_info("Creating mod ZIP package...")
    zip_path = os.path.join(DIST_DIR, "mwco-mod.zip")
    create_zip(os.path.join(BUILD_DIR, "mod"), zip_path)
    size = os.path.getsize(zip_path) / (1024*1024)
    print_success(f"Mod package created ({size:.1f} MB)")
    
    # Copy/create installer script
    print_info("Creating installer script...")
    if is_windows():
        installer = os.path.join(DIST_DIR, "install-mod.bat")
        with open(installer, "w") as f:
            f.write("""@echo off
setlocal enabledelayedexpansion

echo.
echo ======================================
echo   MWCO - My Winter Car Online
echo   Mod Installer
echo ======================================
echo.

REM Try common game installation locations
set GAME_DIR=
if exist "%ProgramFiles(x86)%\Steam\steamapps\common\My Winter Car" (
    set GAME_DIR=%ProgramFiles(x86)%\Steam\steamapps\common\My Winter Car
)
if not defined GAME_DIR (
    echo Error: My Winter Car not found
    echo.
    echo Please make sure My Winter Car is installed and Steam is configured properly.
    pause
    exit /b 1
)

echo Found My Winter Car at: !GAME_DIR!

if not exist "!GAME_DIR!\BepInEx" (
    echo.
    echo Error: BepInEx not found!
    echo.
    echo Please install BepInEx 5.x first:
    echo   1. Download from: https://github.com/BepInEx/BepInEx/releases
    echo   2. Extract to: !GAME_DIR!
    echo   3. Run the game once
    echo   4. Run this installer again
    echo.
    pause
    exit /b 1
)

echo Extracting mod files...
set SCRIPT_DIR=%~dp0
powershell -Command "Add-Type -AssemblyName 'System.IO.Compression.FileSystem'; [System.IO.Compression.ZipFile]::ExtractToDirectory('!SCRIPT_DIR!mwco-mod.zip', '!GAME_DIR!')"

if errorlevel 1 (
    echo.
    echo Error: Extraction failed
    pause
    exit /b 1
)

echo.
echo ======================================
echo   MWCO Mod Installed Successfully!
echo ======================================
echo.
echo Next steps:
echo   1. Run: mwco-server.exe
echo   2. Launch My Winter Car
echo   3. Press F10 in-game to connect
echo.
pause
""")
    else:
        installer = os.path.join(DIST_DIR, "install-mod.sh")
        with open(installer, "w") as f:
            f.write("""#!/bin/bash
set -e
GAME_DIR="$HOME/.local/share/Steam/steamapps/common/My Winter Car"
if [ ! -d "$GAME_DIR/BepInEx" ]; then
    echo "Error: BepInEx not found in $GAME_DIR"
    echo "Please install BepInEx 5.x first"
    exit 1
fi
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
unzip -o "$SCRIPT_DIR/mwco-mod.zip" -d "$GAME_DIR"
echo "✓ MWCO mod installed successfully!"
echo "  Start the server: ./mwco-server"
echo "  Launch the game and press F10 to connect"
""")
        os.chmod(installer, 0o755)
    
    print_success("Installer script created")
    
    # Summary
    print()
    print(f"{Colors.GREEN}{'='*50}")
    print("  BUILD COMPLETE!")
    print(f"{'='*50}{Colors.NC}")
    print()
    print("Distribution files created in 'dist/' directory:")
    print()
    print("1. SERVER (Runnable):")
    print(f"   File: dist/mwco-server{exe_ext}")
    print(f"   Usage: mwco-server{exe_ext} [port]")
    print()
    print("2. MOD PACKAGE (Install into game):")
    print(f"   File: dist/mwco-mod.zip")
    print(f"   Installer: dist/install-mod{'.bat' if is_windows() else '.sh'}")
    print()
    print("Next steps:")
    print(f"  1. Run server:    mwco-server{exe_ext}")
    print(f"  2. Install mod:   install-mod{'.bat' if is_windows() else '.sh'}")
    print()

if __name__ == "__main__":
    main()
