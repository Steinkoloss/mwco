# MWCO Build Instructions

## Overview
You have 2 distributable outputs:
1. **mwco-server** - A standalone executable to run the multiplayer server
2. **mwco-mod** - A mod package to install into your game

## Build Steps

### Step 1: Build the Solution
```bash
cd /workspaces/mwco
dotnet build MWCO.slnx --configuration Release
```

### Step 2: Publish Server as Standalone Executable
```bash
dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output dist/server
```

The executable will be at: `dist/server/MWCO.Server`

### Step 3: Prepare Mod Package
The mod DLLs are built in:
- `MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll`
- `MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll`
- `MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll`

Create package structure:
```
mwco-mod/
└── BepInEx/
    └── plugins/
        ├── MWCO.Client.dll
        ├── MWCO.Shared.dll
        └── 0Harmony.dll
```

### Step 4: Create Distribution Package
Create a tarball: `mwco-mod.tar.gz`

## Installation

### Server
Simply run the executable:
```bash
./mwco-server
```

### Mod
1. Extract the mod package into your game directory
2. It will merge with existing BepInEx folder
3. Run the server
4. Launch the game
5. Press F10 to connect
