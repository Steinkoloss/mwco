# MWCO Windows Build Guide

## The Easy Way - Just Run This!

Open **Command Prompt** or **PowerShell** in your MWCO project folder and run:

```cmd
python build.py
```

Or if you prefer batch script:

```cmd
build-dist.bat
```

That's it! The script will:
1. ✅ Build everything
2. ✅ Create `mwco-server.exe` (the server)
3. ✅ Create `mwco-mod.zip` (the mod)
4. ✅ Create `install-mod.bat` (easy installer)
5. ✅ Put everything in `dist\` folder

---

## What You Get

### 1. Server: `dist\mwco-server.exe`
- Just double-click to run the server
- Or from command line: `mwco-server.exe`
- Server will start on port 1999

### 2. Mod: `dist\mwco-mod.zip` + `dist\install-mod.bat`
- Double-click `install-mod.bat` to install the mod
- It automatically finds your game and installs everything

---

## Prerequisites

Before building, make sure you have:

1. **.NET SDK 10.0+** 
   - Download from: https://dotnet.microsoft.com/download
   - Check you have it: Open Command Prompt and run `dotnet --version`

2. **My Winter Car** 
   - Installed from Steam

That's literally all you need!

---

## After Building

### To Run the Server:
```cmd
cd dist
mwco-server.exe
```

Server is now running! Tell your friends the address (e.g., `YOUR_IP:1999`)

### To Install the Mod:
```cmd
cd dist
install-mod.bat
```

Follow the prompts and you're done!

---

## Distributing Files

### Share the Server:
- Just copy `dist\mwco-server.exe` to someone else
- They run it - that's it!

### Share the Mod:
- Copy both `dist\mwco-mod.zip` AND `dist\install-mod.bat` together
- They run `install-mod.bat` - done!

### Share Everything:
- Zip up all three files (`mwco-server.exe`, `mwco-mod.zip`, `install-mod.bat`)
- Share with friends

---

## Troubleshooting

### "dotnet is not recognized"
- You need to install .NET SDK from https://dotnet.microsoft.com/download
- After installing, restart your command prompt

### "Build failed"
- Make sure you're in the MWCO project root folder (where `MWCO.slnx` is)
- Try: `dotnet build MWCO.slnx --configuration Release`

### "Can't find My Winter Car"
- Make sure it's installed from Steam
- Check the installer knows where to look (it searches common Steam paths)

### Server won't start
- Check port 1999 isn't already in use
- Try a different port: `mwco-server.exe 2000`

### Mod won't install
- Make sure BepInEx is installed in your game directory
- Try running `install-mod.bat` as Administrator
- Check both files (`mwco-mod.zip` and `install-mod.bat`) are in the same folder

---

## File Sizes

- `mwco-server.exe`: ~80 MB (includes everything needed to run)
- `mwco-mod.zip`: ~8 MB
- Total: ~88 MB for both

---

## Next Steps

1. Run: `python build.py` (or `build-dist.bat`)
2. Wait for it to finish
3. Check the `dist\` folder - you should see 3 files
4. You're ready to go!

For more details, see [COMPILE.md](COMPILE.md) or [DISTRIBUTION.md](DISTRIBUTION.md)
