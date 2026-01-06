# MWCO - Build & Distribution Guide

## Quick Start

To compile MWCO into 2 distributable files, run this command from the project root:

```bash
bash build-dist.sh
```

This will create two files in the `dist/` directory:
1. **mwco-server** - Standalone server executable (runnable)
2. **mwco-mod.tar.gz** - Mod package (install into game)

---

## What You Get

### 1. Server Executable: `dist/mwco-server`
- A self-contained Linux executable
- Runs the multiplayer server
- No dependencies needed (everything is included)
- Usage: `./dist/mwco-server [port]`
- Default port: 1999

**To share the server:**
- Just copy `dist/mwco-server` to another machine
- Make it executable: `chmod +x mwco-server`
- Run it: `./mwco-server`

### 2. Mod Package: `dist/mwco-mod.tar.gz` + `dist/install-mod.sh`
- Complete mod package ready for distribution
- Includes automatic installer script
- Simply requires extracting into game directory

**To share the mod:**
- Copy both `mwco-mod.tar.gz` and `install-mod.sh` together
- User runs: `./install-mod.sh`
- That's it! Mod is installed automatically

---

## Installation Instructions for End Users

### For the Server Owner

```bash
# Build both files
bash build-dist.sh

# Run the server
./dist/mwco-server

# Server is now running on port 1999
# Share the server address with your players
```

### For the Mod Users

**Option 1: Automatic Installation (Recommended)**
```bash
# Run the installer script
./install-mod.sh

# Done! Mod is installed
```

**Option 2: Manual Installation**
```bash
# Extract into your game directory
tar -xzf mwco-mod.tar.gz -C ~/.local/share/Steam/steamapps/common/"My Winter Car"/

# Done! Files are merged into BepInEx folder
```

### Playing the Game

1. Start the MWCO server (server owner's responsibility)
2. Launch My Winter Car
3. Press **F10** to open the MWCO connection menu
4. Enter the server address (e.g., `127.0.0.1:1999` for local, or `your.server.ip:1999` for remote)
5. Click **Connect**
6. Start driving with other players!

---

## Build Requirements

- .NET SDK 10.0 or later
- Linux environment (tested on Ubuntu 24.04)
- About 500MB of free disk space (for build artifacts)

## Files Explained

### In `dist/` directory after building:

- **mwco-server** (60-100 MB)
  - Self-contained executable
  - Include all .NET runtime and dependencies
  - Can be run on any Linux system

- **mwco-mod.tar.gz** (5-10 MB)
  - Compressed mod package
  - Contains BepInEx plugin DLLs

- **install-mod.sh**
  - Simple bash script
  - Automatically installs mod into game directory
  - Checks for prerequisites

---

## Customization

### Change Server Port

Run the server with a custom port:
```bash
./dist/mwco-server 2000
```

### Build Only (No Distribution)

Just build without packaging:
```bash
dotnet build MWCO.slnx --configuration Release
```

### Manual Steps (If build-dist.sh doesn't work)

If the automated script has issues, you can build manually:

```bash
# 1. Build solution
dotnet build MWCO.slnx --configuration Release

# 2. Publish server as standalone
dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output dist/server

# 3. Copy mod DLLs
mkdir -p dist/mod/BepInEx/plugins
cp MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll dist/mod/BepInEx/plugins/
cp MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll dist/mod/BepInEx/plugins/
cp MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll dist/mod/BepInEx/plugins/

# 4. Create tarball
cd dist && tar -czf mwco-mod.tar.gz mod/ && cd ..
```

---

## Troubleshooting

### Server won't start
- Check port 1999 is not in use: `netstat -an | grep 1999`
- Try a different port: `./mwco-server 2000`

### Mod won't install
- Verify BepInEx is installed in the game directory
- Check file permissions: `chmod +x install-mod.sh`
- Run installer with sudo if permission denied

### Players can't connect
- Firewall might be blocking UDP port 1999
- Check server is running: `netstat -an | grep 1999`
- Verify player has correct server IP/port

---

## Distribution to Others

### For a Server Binary:
Package the server executable and instructions:
```
MWCO-Server/
├── mwco-server
└── README.txt (with instructions)
```

### For a Game Mod:
Package the mod and installer:
```
MWCO-Mod/
├── install-mod.sh
├── mwco-mod.tar.gz
└── README.txt (with installation steps)
```

---

## File Sizes (Approximate)

- mwco-server: ~80 MB (includes .NET runtime)
- mwco-mod.tar.gz: ~8 MB (compressed)
- Total: ~88 MB for both

---

## Next Steps

1. Run: `bash build-dist.sh`
2. Wait for the build to complete
3. Check `dist/` directory for the two files
4. Share or run as needed!

For detailed technical information, see README.md and PROJECT_OVERVIEW.md
