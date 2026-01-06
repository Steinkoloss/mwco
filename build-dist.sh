#!/bin/bash

# MWCO Complete Build & Distribution Script
# This script builds both the server executable and mod package
# for distribution to other users

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

BUILD_DIR="build"
DIST_DIR="dist"

print_header() {
    echo -e "${GREEN}======================================"
    echo "  MWCO - My Winter Car Online"
    echo "  Complete Build & Distribution"
    echo "======================================${NC}"
    echo
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}→ $1${NC}"
}

print_header

# Verify we're in the right directory
if [ ! -f "MWCO.slnx" ]; then
    print_error "Not in MWCO root directory. Please run this from /path/to/mwco"
    exit 1
fi

# Step 1: Clean and prepare directories
print_info "Cleaning previous builds..."
rm -rf "$BUILD_DIR" "$DIST_DIR"
mkdir -p "$BUILD_DIR" "$DIST_DIR"
print_success "Directories prepared"

# Step 2: Build the entire solution
echo
print_info "Building MWCO solution (Release mode)..."
if dotnet build MWCO.slnx --configuration Release -v minimal; then
    print_success "Solution built successfully"
else
    print_error "Failed to build solution"
    exit 1
fi

# Step 3: Create Server Distribution
echo
print_info "Creating server distribution..."
mkdir -p "$BUILD_DIR/server"

if dotnet publish MWCO.Server/MWCO.Server/MWCO.Server.csproj \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output "$BUILD_DIR/server" \
    -v minimal; then
    print_success "Server published"
else
    print_error "Failed to publish server"
    exit 1
fi

# Copy and rename executable
if [ -f "$BUILD_DIR/server/MWCO.Server" ]; then
    cp "$BUILD_DIR/server/MWCO.Server" "$DIST_DIR/mwco-server"
    chmod +x "$DIST_DIR/mwco-server"
    print_success "Server executable: dist/mwco-server"
else
    print_error "Server executable not found"
    exit 1
fi

# Step 4: Create Mod Package
echo
print_info "Creating mod distribution package..."
mkdir -p "$BUILD_DIR/mod/BepInEx/plugins"

# Check and copy DLLs
if [ ! -f "MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll" ]; then
    print_error "MWCO.Client.dll not found"
    exit 1
fi
cp "MWCO.Client/bin/Release/netstandard2.1/MWCO.Client.dll" "$BUILD_DIR/mod/BepInEx/plugins/"
print_success "Copied MWCO.Client.dll"

if [ ! -f "MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll" ]; then
    print_error "MWCO.Shared.dll not found"
    exit 1
fi
cp "MWCO.Shared/bin/Release/netstandard2.1/MWCO.Shared.dll" "$BUILD_DIR/mod/BepInEx/plugins/"
print_success "Copied MWCO.Shared.dll"

# Copy Harmony if it exists
if [ -f "MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll" ]; then
    cp "MWCO.Client/bin/Release/netstandard2.1/0Harmony.dll" "$BUILD_DIR/mod/BepInEx/plugins/"
    print_success "Copied 0Harmony.dll"
fi

# Create README
cat > "$BUILD_DIR/mod/README.txt" << 'READMEOF'
MWCO - My Winter Car Online Mod
================================

INSTALLATION STEPS:

1. PREREQUISITE: Install BepInEx
   If you don't have BepInEx installed yet:
   - Download BepInEx 5.x from:
     https://github.com/BepInEx/BepInEx/releases
   - Extract to your game directory:
     ~/.local/share/Steam/steamapps/common/My Winter Car/
   - Run the game once to initialize

2. INSTALL THE MOD:
   Option A - Automatic (Recommended):
   - Run the included install-mod.sh script:
     ./install-mod.sh

   Option B - Manual:
   - Extract this package's BepInEx folder to your game directory
   - Merge with existing BepInEx folder

3. START THE SERVER:
   - Run the mwco-server executable:
     ./mwco-server
   - Default port: 1999

4. PLAY:
   - Launch My Winter Car from Steam
   - Wait for full game load
   - Press F10 to open MWCO menu
   - Enter server address (default: 127.0.0.1:1999)
   - Click Connect

TROUBLESHOOTING:
- If mod doesn't load, check BepInEx is installed
- If connection fails, verify server is running
- Check game console (F12) for errors

For more information, visit the project repository.
READMEOF

# Create tarball
print_info "Creating mod package tarball..."
cd "$BUILD_DIR"
tar -czf "../$DIST_DIR/mwco-mod.tar.gz" mod/
cd - > /dev/null
print_success "Mod package: dist/mwco-mod.tar.gz"

# Step 5: Create Installer Script
print_info "Creating mod installer script..."
cat > "$DIST_DIR/install-mod.sh" << 'INSTALLEEOF'
#!/bin/bash

# MWCO Mod Installer
# Automatically installs MWCO mod into your game

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

print_header() {
    echo -e "${GREEN}======================================"
    echo "  MWCO - My Winter Car Online"
    echo "  Mod Installer"
    echo "======================================${NC}"
    echo
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
    exit 1
}

print_info() {
    echo -e "${YELLOW}→ $1${NC}"
}

print_header

# Find game directory
GAME_DIR="$HOME/.local/share/Steam/steamapps/common/My Winter Car"

if [ ! -d "$GAME_DIR" ]; then
    print_error "My Winter Car not found at: $GAME_DIR"
    echo "Please install My Winter Car from Steam first."
    exit 1
fi

print_success "Found My Winter Car at: $GAME_DIR"

# Check for BepInEx
if [ ! -d "$GAME_DIR/BepInEx" ]; then
    echo
    print_error "BepInEx not found in game directory"
    echo
    echo "Please install BepInEx 5.x first:"
    echo "  1. Download from: https://github.com/BepInEx/BepInEx/releases"
    echo "  2. Extract to: $GAME_DIR"
    echo "  3. Run the game once to initialize"
    echo "  4. Run this installer again"
    exit 1
fi

print_success "Found BepInEx installation"

# Install mod
echo
print_info "Installing MWCO mod files..."

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ ! -f "$SCRIPT_DIR/mwco-mod.tar.gz" ]; then
    print_error "mwco-mod.tar.gz not found in: $SCRIPT_DIR"
    exit 1
fi

# Extract to game directory
tar -xzf "$SCRIPT_DIR/mwco-mod.tar.gz" -C "$GAME_DIR" --no-same-owner

print_success "Mod files installed"

# Verify installation
if [ -f "$GAME_DIR/BepInEx/plugins/MWCO.Client.dll" ]; then
    print_success "MWCO.Client.dll installed"
else
    print_error "Installation verification failed"
    exit 1
fi

echo
echo "======================================"
echo -e "${GREEN}MWCO Mod Installed Successfully!${NC}"
echo "======================================"
echo
echo "Next steps:"
echo "  1. Start the MWCO server:"
echo "     ./mwco-server"
echo
echo "  2. Launch My Winter Car from Steam"
echo
echo "  3. Press F10 in-game to open connection menu"
echo
echo "  4. Enter server address (default: 127.0.0.1:1999)"
echo
echo "  5. Click Connect and start playing!"
echo
echo "For support, see README in the mod package."
echo

INSTALLEEOF

chmod +x "$DIST_DIR/install-mod.sh"
print_success "Installer script: dist/install-mod.sh"

# Final summary
echo
echo "======================================"
echo -e "${GREEN}BUILD COMPLETE!${NC}"
echo "======================================"
echo
echo "DISTRIBUTION FILES:"
echo
echo "1. SERVER EXECUTABLE (Run the multiplayer server):"
echo "   File: dist/mwco-server"
echo "   Usage: ./dist/mwco-server"
echo "   Size: $(du -h dist/mwco-server | cut -f1)"
echo
echo "2. MOD PACKAGE (Install into game):"
echo "   File: dist/mwco-mod.tar.gz"
echo "   Size: $(du -h dist/mwco-mod.tar.gz | cut -f1)"
echo
echo "3. INSTALLER SCRIPT (Easy mod installation):"
echo "   File: dist/install-mod.sh"
echo "   Usage: ./dist/install-mod.sh"
echo
echo "======================================"
echo "DISTRIBUTION INSTRUCTIONS:"
echo "======================================"
echo
echo "FOR RUNNING THE SERVER:"
echo "  → Copy dist/mwco-server to your server machine"
echo "  → Make it executable: chmod +x mwco-server"
echo "  → Run it: ./mwco-server"
echo
echo "FOR INSTALLING THE MOD:"
echo "  Option 1 (Recommended):"
echo "    → Copy dist/mwco-mod.tar.gz and dist/install-mod.sh"
echo "    → Run: ./install-mod.sh"
echo
echo "  Option 2 (Manual):"
echo "    → Extract dist/mwco-mod.tar.gz into game directory"
echo "    → Merge with existing BepInEx folder"
echo
