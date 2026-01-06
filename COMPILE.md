# MWCO Compilation Guide - Two-File Distribution

## Summary

I've prepared your MWCO project to compile into exactly 2 distributable files:

1. **`mwco-server.exe`** - A standalone runnable server executable
2. **`mwco-mod.zip`** - A packaged mod for easy game installation

Plus an auto-installer script: **`install-mod.bat`**

---

## Quick Build Instructions

Navigate to your MWCO project root and run ONE of these:

### Option 1: Python (Recommended - Works on Windows & Linux)
```cmd
python build.py
```

### Option 2: Windows Batch Script
```cmd
build-dist.bat
```

Both scripts will:
1. Clean previous builds
2. Compile the entire solution in Release mode
3. Create a self-contained server executable
4. Package the mod DLLs into a ZIP file
5. Create an installer script
6. Place everything in `dist/` directory

---

## What Gets Built

### File 1: `dist\mwco-server.exe` (~80 MB)
- **Type:** Standalone Windows executable
- **What it does:** Runs the My Winter Car Online multiplayer server
- **How to use:** Just double-click it or run: `mwco-server.exe` (optionally with port: `mwco-server.exe 2000`)
- **Distribution:** Just copy this one file to any Windows machine and run it

### File 2: `dist\mwco-mod.zip` (~8 MB)
- **Type:** Compressed mod package
- **What it contains:** All necessary DLLs for the game mod
- **How to use:** Run `dist\install-mod.bat` or manually extract into game directory
- **Distribution:** Copy both the .zip and install-mod.bat script together

### File 3: `dist\install-mod.bat` (Helper)
- **Type:** Windows batch installation script
- **What it does:** Automatically installs mod into your game directory
- **How to use:** Just double-click it

---

## Changes Made to Project

### 1. Updated Server Project Configuration
**File:** `MWCO.Server/MWCO.Server/MWCO.Server.csproj`

Added self-contained publishing options:
```xml
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

This allows the server to be published as a standalone executable that includes the entire .NET runtime - no dependencies needed!

### 2. Created Build Scripts
- **`build-dist.bat`** - Windows batch build script with colored output
- **`build.py`** - Python build script (works on Windows & Linux)

### 3. Created Documentation
- **`DISTRIBUTION.md`** - Complete distribution and deployment guide
- **`BUILD_INSTRUCTIONS.md`** - Technical build reference

---

## Manual Build Steps (If Scripts Don't Work)

If the automated scripts have issues, you can build manually:

### Step 1: Build the Solution
```cmd
cd C:\path\to\mwco
dotnet build MWCO.slnx --configuration Release
```

### Step 2: Publish Server as Standalone
```cmd
dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj ^
    --configuration Release ^
    --self-contained ^
    --runtime win-x64 ^
    --output dist/server
```

### Step 3: Set Up Mod Package
```cmd
mkdir dist\mod\BepInEx\plugins

REM Copy DLLs
copy MWCO.Client\bin\Release\netstandard2.1\MWCO.Client.dll dist\mod\BepInEx\plugins\
copy MWCO.Shared\bin\Release\netstandard2.1\MWCO.Shared.dll dist\mod\BepInEx\plugins\
copy MWCO.Client\bin\Release\netstandard2.1\0Harmony.dll dist\mod\BepInEx\plugins\

REM Copy server executable
copy dist\server\MWCO.Server.exe dist\mwco-server.exe
```

### Step 4: Create Mod Package (ZIP)
Use Windows Explorer to create `mwco-mod.zip` from the `dist\mod` folder, or use PowerShell:
```powershell
Add-Type -AssemblyName "System.IO.Compression.FileSystem"
[System.IO.Compression.ZipFile]::CreateFromDirectory("dist\mod", "dist\mwco-mod.zip")
```

---

## Usage Instructions for End Users

### For Someone Running the Server:

```cmd
REM Download mwco-server.exe
REM Just run it!
mwco-server.exe

REM Server is now listening on port 1999
REM Share your server IP with players (e.g., your.ip.address:1999)
```

### For Someone Installing the Mod:

**Method 1: Automatic (Recommended)**
```cmd
REM Run the installer
install-mod.bat
```

**Method 2: Manual**
```cmd
REM Extract the ZIP into your game directory
REM Right-click mwco-mod.zip > Extract All
REM Select your My Winter Car game directory
```

Then:
1. Launch My Winter Car from Steam
2. Press **F10** in-game to open connection menu
3. Enter server IP (e.g., `127.0.0.1:1999` or `your.server.ip:1999`)
4. Click Connect and play!

---

## Project Structure After Build

```
C:\path\to\mwco\
├── dist\                              # Distribution files
│   ├── mwco-server.exe               # Server executable (runnable)
│   ├── mwco-mod.zip                  # Mod package
│   ├── install-mod.bat               # Mod installer script
│   └── mod\                           # Extracted mod structure
│       └── BepInEx\
│           └── plugins\
│               ├── MWCO.Client.dll
│               ├── MWCO.Shared.dll
│               └── 0Harmony.dll
│
├── build\                             # Build intermediates
│   ├── server\                        # Server build output
│   │   └── (all server build files)
│   └── mod\                           # Mod package before compression
│
└── (rest of project files...)
```

---

## Sharing Files with Others

### To Share the Server:
- Give them: `dist\mwco-server.exe`
- Instructions: "Just run it!"

### To Share the Mod:
- Give them: Both `dist\mwco-mod.zip` AND `dist\install-mod.bat` (keep together)
- Instructions: "Double-click install-mod.bat and follow the prompts"

### To Share Everything:
- Create a package with:
  ```
  MWCO-Package\
  ├── mwco-server.exe
  ├── mwco-mod.zip
  ├── install-mod.bat
  └── README.txt (with usage instructions)
  ```

---

## Build Requirements

- .NET SDK 10.0+ (test with `dotnet --version`)
- Windows 10/11 OR Linux (Ubuntu 20.04+)
- ~1 GB free disk space
- Python 3 (for build.py)

## Build Time
- Full build: 1-3 minutes (depends on system)
- Incremental builds: Much faster

---

## Next Steps

1. Run one of the build scripts:
   ```cmd
   python build.py
   REM or
   build-dist.bat
   ```

2. Wait for completion

3. Check `dist\` directory for your files:
   ```cmd
   dir dist\
   ```

4. Done! You now have:
   - **A runnable server** → Just run `mwco-server.exe`
   - **A mod package** → Install with `install-mod.bat`

---

## Troubleshooting

### Build fails with "missing DLLs"
- Ensure game is installed correctly
- Check MWCO.Client.csproj references correct game DLL paths

### Server won't start
- Check port availability: `netstat -ano | findstr :1999`
- Try custom port: `mwco-server.exe 2000`
- Make sure .NET is installed: `dotnet --version`

### Mod installer fails
- Ensure BepInEx is installed in game directory
- Try running as Administrator
- Verify the mwco-mod.zip and install-mod.bat are in the same folder

---

## See Also
- [DISTRIBUTION.md](DISTRIBUTION.md) - Detailed distribution guide
- [BUILD_INSTRUCTIONS.md](BUILD_INSTRUCTIONS.md) - Technical reference
- [QUICKSTART.md](QUICKSTART.md) - Quick start guide
- [README.md](README.md) - Main documentation
