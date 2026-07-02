# Elden Ring AIO Launcher

An all-in-one custom launcher for Elden Ring that safely manages and launches the vanilla game, Seamless Co-op, and The Convergence mod—all from a single clean interface without triggering anti-cheat or risking bans.

## Features

- **Vanilla Safe:** Automatically manages `start_protected_game.exe` to ensure you can still launch the official Vanilla game safely for official servers.
- **Seamless Co-op Support:** Perfectly bypasses Steam Job Object tracking and CWD bugs in the Seamless Co-op executable to provide a flawless 1-click launch.
- **The Convergence Support:** Integrates seamlessly with Mod Engine 2 to launch The Convergence flawlessly.
- **Zero Virus Flags:** Built natively using Nuitka for C-level compilation to ensure antivirus software doesn't falsely flag the executable (a common issue with PyInstaller).
- **Clean Uninstaller:** Includes a self-cleaning uninstaller that safely restores your original game files and leaves no trace on your system.

## Setup Instructions

1. Download the latest `EldenRingAIOLauncher.exe` from the Releases page (or the `output` folder).
2. Place it anywhere on your PC (or directly in your Elden Ring `Game` folder).
3. Run it, and use the setup wizard to locate your `start_protected_game.exe` (Vanilla), `ersc_launcher.exe` (Seamless Co-op), and `Start_Convergence.bat` (Convergence).
4. You're done! The launcher handles the rest.

## Building from Source

To compile the launcher yourself, ensure you have Python installed with the required packages:

```bash
pip install -r requirements.txt
python build.py
```

The output executable will be placed in the `output/` directory.

---
*Created by Dante69K*
