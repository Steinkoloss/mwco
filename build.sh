#!/bin/bash

# MWCO Build Script - Compiles server executable and mod package

set -e

BUILD_DIR="build"
DIST_DIR="dist"

echo "======================================"
echo "  MWCO - My Winter Car Online"
echo "  Build Script"
echo "======================================"
echo

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$BUILD_DIR" "$DIST_DIR"
mkdir -p "$BUILD_DIR" "$DIST_DIR"

# Build the solution
echo "Building MWCO solution..."
dotnet build MWCO.slnx --configuration Release

# Create server distribution
echo
echo "Creating server distribution..."
mkdir -p "$BUILD_DIR/server"

# Publish server as self-contained executable
dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output "$BUILD_DIR/server"

# Copy server executable to dist with executable name
if [ -f "$BUILD_DIR/server/MWCO.Server" ]; then
    cp "$BUILD_DIR/server/MWCO.Server" "$DIST_DIR/mwco-server"
    chmod +x "$DIST_DIR/mwco-server"
    echo "✓ Server executable created: dist/mwco-server"
else
    echo "Error: Server executable not found!"
    exit 1
fi

# Create mod package
echo
echo "Creating mod package..."
mkdir -p "$BUILD_DIR/mod/BepInEx/plugins"

# Copy client DLLs
if [ -f "MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll" ]; then
    cp "MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll" "$BUILD_DIR/mod/BepInEx/plugins/"
    echo "✓ Copied MWCO.Client.dll"
else
    echo "Error: MWCO.Client.dll not found!"
    exit 1
fi

if [ -f "MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll" ]; then
    cp "MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll" "$BUILD_DIR/mod/BepInEx/plugins/"
    echo "✓ Copied MWCO.Shared.dll"
else
    echo "Error: MWCO.Shared.dll not found!"
    exit 1
fi

# Copy Harmony dependency
find "$BUILD_DIR/mod" -name "0Harmony.dll" -delete 2>/dev/null || true
if [ -f "MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll" ]; then
    cp "MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll" "$BUILD_DIR/mod/BepInEx/plugins/"
    echo "✓ Copied 0Harmony.dll"
fi

# Create a README for the mod package
cat > "$BUILD_DIR/mod/INSTALL.txt" << 'EOF'
MWCO Mod Installation
=====================

1. Make sure you have BepInEx 5.x installed in your My Winter Car game directory:
   ~/.local/share/Steam/steamapps/common/My Winter Car/BepInEx/

   If you don't have BepInEx:
   - Download from: https://github.com/BepInEx/BepInEx/releases
   - Extract to your game directory
   - Run the game once to initialize it

2. Copy the BepInEx folder from this package into your My Winter Car directory,
   merging with existing files. This will copy the mod DLLs into:
   ~/.local/share/Steam/steamapps/common/My Winter Car/BepInEx/plugins/

3. Start the MWCO server (mwco-server executable)

4. Launch My Winter Car

5. Press F10 in-game to open the MWCO connection menu

6. Enter your server address (default: 127.0.0.1:1999) and click Connect

Done! You're now playing My Winter Car Online!
EOF

# Create tarball of mod
cd "$BUILD_DIR"
tar -czf "../$DIST_DIR/mwco-mod.tar.gz" mod/
cd - > /dev/null

echo "✓ Mod package created: dist/mwco-mod.tar.gz"

# Create simple installer script in the package
cat > "$DIST_DIR/install-mod.sh" << 'EOF'
#!/bin/bash

# Simple MWCO Mod Installer

set -e

echo "======================================"
echo "  MWCO - My Winter Car Online"
echo "  Mod Installer"
echo "======================================"
echo

# Determine game directory
GAME_DIR="$HOME/.local/share/Steam/steamapps/common/My Winter Car"

if [ ! -d "$GAME_DIR" ]; then
    echo "Error: My Winter Car not found at:"
    echo "  $GAME_DIR"
    echo
    echo "Please install My Winter Car from Steam first."
    exit 1
fi

echo "Found My Winter Car at: $GAME_DIR"

# Check for BepInEx
if [ ! -d "$GAME_DIR/BepInEx" ]; then
    echo
    echo "Error: BepInEx not found!"
    echo
    echo "Please install BepInEx 5.x first:"
    echo "  1. Download from: https://github.com/BepInEx/BepInEx/releases"
    echo "  2. Extract to: $GAME_DIR"
    echo "  3. Run the game once"
    echo "  4. Run this installer again"
    exit 1
fi

# Extract and install
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -f "$SCRIPT_DIR/mwco-mod.tar.gz" ]; then
    echo
    echo "Extracting mod files..."
    tar -xzf "$SCRIPT_DIR/mwco-mod.tar.gz" -C "$GAME_DIR"
    
    echo
    echo "======================================"
    echo "  MWCO Mod Installed Successfully!"
    echo "======================================"
    echo
    echo "Next steps:"
    echo "  1. Run the MWCO server: $SCRIPT_DIR/mwco-server"
    echo "  2. Launch My Winter Car from Steam"
    echo "  3. Press F10 in-game to connect"
    echo
else
    echo "Error: mwco-mod.tar.gz not found in $(dirname "$SCRIPT_DIR")"
    exit 1
fi
EOF

chmod +x "$DIST_DIR/install-mod.sh"

# Summary
echo
echo "======================================"
echo "  Build Complete!"
echo "======================================"
echo
echo "Distribution files created in 'dist/' directory:"
echo
echo "1. SERVER (Runnable):"
echo "   dist/mwco-server"
echo "   - Start the multiplayer server"
echo "   - Usage: ./dist/mwco-server [port]"
echo "   - Default port: 1999"
echo
echo "2. MOD PACKAGE (Install into game):"
echo "   dist/mwco-mod.tar.gz"
echo "   dist/install-mod.sh"
echo "   - Simple installer script to deploy mod"
echo "   - Usage: ./dist/install-mod.sh"
echo
echo "Next steps:"
echo "  1. To run the server:"
echo "     ./dist/mwco-server"
echo
echo "  2. To install the mod:"
echo "     ./dist/install-mod.sh"
echo
