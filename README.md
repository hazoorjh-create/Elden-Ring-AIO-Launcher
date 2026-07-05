# Elden Ring AIO Launcher
**Created by Dante69K**

An all-in-one custom launcher for Elden Ring that safely manages and launches the vanilla game, Seamless Co-op, and The Convergence mod—all from a single clean interface without triggering anti-cheat or risking bans.

## FEATURES
- **Vanilla Safe:** Automatically manages `start_protected_game.exe` to ensure you can still launch the official Vanilla game safely for official servers.
- **Seamless Co-op Support:** Perfectly bypasses Steam Job Object tracking and CWD bugs in the Seamless Co-op executable to provide a flawless 1-click launch.
- **The Convergence Support:** Integrates seamlessly to launch The Convergence flawlessly.
- **Offline Mode Support:** Automatically detects and plays via the EldenRingOfflineLauncher.
- **Zero Virus Flags:** Built natively in C# utilizing safe Windows Explorer routing to ensure antivirus software (like Windows Defender) doesn't falsely flag the executable as a Trojan (Wacatac).
  - [VirusTotal Scan](https://www.virustotal.com/gui/file/f2f41d3d5e3afa967e8bda419b99f797ca3c5dcf909ce6365db3e1d937a237fe)
- **Clean Uninstaller:** Includes a self-cleaning uninstaller inside the app that safely restores your original game files and leaves no trace on your system.

## SETUP INSTRUCTIONS
1. Extract `EldenRingAIOLauncher.exe` from this zip directly into your Elden Ring "Game" folder (where your `start_protected_game.exe` is located).
2. Run the game normally through Steam. The Setup Wizard will automatically pop up, safely back up your Vanilla executable, and install the launcher.
3. You're done! The launcher handles the rest.

## CREDITS & ATTRIBUTIONS
"Rites" Kevin MacLeod (incompetech.com)  
Licensed under Creative Commons: By Attribution 4.0 License  
http://creativecommons.org/licenses/by/4.0/

---

## Architecture & Technical Summary

This section provides a technical overview of how the Elden Ring AIO (All-In-One) Launcher functions. It is primarily intended for NexusMods reviewers and open-source contributors to understand the mechanics under the hood.

### 1. How the Program Works (Installation & Setup)
The program is a C# Windows Presentation Foundation (WPF) application designed to act as a centralized hub for launching various Elden Ring mods (Seamless Co-op, The Convergence, Offline Mode, etc.).
* **Installation Mechanics:** During setup, the user selects their game directory. The launcher renames the original `start_protected_game.exe` to `real_start_protected_game.exe` and copies its own executable in its place as the new `start_protected_game.exe`.
* **State Management:** It maintains a `launcher_config.json` file in the game directory. This file stores the paths to the user's mod executables, custom mod configurations, and UI preferences (such as auto-close behavior and audio mute settings).

### 2. General Launch Mechanism (Escaping Steam's Sandbox)
A critical challenge in launching Elden Ring mods via a third-party application launched through Steam is that Steam bounds child processes in a Job Object. Additionally, some mods like Seamless Co-op will instantly abort if they detect the launcher is still running in memory.
To bypass these issues, the launcher uses a specific delayed execution method:
1. It generates a temporary batch script (`launch_mod_delayed.bat`) in the user's Temp folder.
2. The batch script uses a `ping 127.0.0.1` delay to wait ~3 seconds.
3. It changes the current working directory (CWD) to the target mod's directory and executes the mod.
4. The launcher executes this script via `explorer.exe` (e.g., `explorer.exe "path\to\script.bat"`), which successfully breaks the process out of Steam's job constraint tree.
5. The launcher then immediately calls `Environment.Exit(0)` to kill itself before the batch script actually starts the game, avoiding any detection by anti-cheat or mods.

### 3. How Vanilla Elden Ring Launches
When the user clicks the **Vanilla** button, the launcher simply bypasses the modding system:
* It directly starts the backed-up `real_start_protected_game.exe`.
* It injects the environment variable `SteamAppId = "1245620"` into the process so that Steam hooks into it correctly.
* The launcher then closes itself.

### 4. How Seamless Co-op Works
The launcher has native integration for Seamless Co-op:
* It auto-detects `ersc_launcher.exe` in the game directory or within a `SeamlessCoop` subfolder. If not found, it prompts the user to manually locate it.
* When launched, it uses the delayed execution script (described in Section 2) to launch the `ersc_launcher.exe`.

### 5. How the Password Sync System Works
To simplify playing with friends across different mod setups (like combining Seamless Co-op with The Convergence), the launcher features a dynamic password synchronization system:
* **Reading:** The launcher UI automatically parses `ersc_settings.ini` to find the `cooppassword = ...` line and displays it in the interface.
* **Writing:** When a user launches a mod with password sync enabled, the launcher reads the target mod's directory, finds its local `ersc_settings.ini`, and overwrites the `cooppassword` line with whatever is currently typed in the launcher's text box.
* This happens instantaneously before the game launches, meaning users can change their password on the fly without ever opening an `.ini` file.

### 6. How Custom Executables Work
Users can add as many unsupported or custom mods as they want:
* Users select a custom `.exe` or `.bat` file, and the launcher saves this entry in the `launcher_config.json` under `CustomMods`.
* The UI dynamically renders a launch card for each custom mod.
* If Seamless Co-op is installed, a "Sync Seamless Password" checkbox appears on every custom mod card.
* When a custom mod is launched, the launcher dynamically creates the delayed-launch script for that specific executable, syncs the password if the checkbox is ticked, and runs it exactly as it would an officially supported mod.
