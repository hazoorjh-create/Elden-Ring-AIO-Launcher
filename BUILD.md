# Build Instructions for Elden Ring AIO Launcher

This document contains detailed instructions on how to build the Elden Ring AIO Launcher from its source code. This is useful for those who want to verify the source code, contribute, or compile the tool themselves (e.g., Nexus Mods administration).

## Prerequisites

To compile the application, you will need the following tools installed on your Windows machine:
1. **.NET 10.0 SDK**: Required to build and publish the C# WPF application. 
   - Download from the [official Microsoft website](https://dotnet.microsoft.com/download/dotnet/10.0).
2. **Visual Studio 2022** (optional but recommended): If you wish to open, edit, and build using an IDE, the Community version is free.

## How to Build via Command Line (CLI)

1. Open a Command Prompt or PowerShell window.
2. Navigate to the directory containing `EldenRingLauncher.csproj`.
3. Run the following command to restore dependencies and publish the executable:

   ```bash
   dotnet publish -c Release -r win-x64
   ```

   - `-c Release`: Compiles the application with optimizations for release.
   - `-r win-x64`: Targets 64-bit Windows environments.

4. Once the command completes successfully, the compiled output will be located in:
   `bin\Release\net10.0-windows\win-x64\publish\`

   You will find `EldenRingAIOLauncher.exe` in this folder. This is a single-file executable (as defined in the `.csproj`).

## How to Build via Visual Studio

1. Open `EldenRingLauncher.csproj` using Visual Studio 2022.
2. At the top toolbar, ensure the build configuration is set to **Release** and platform to **Any CPU** (or x64).
3. Right-click on the `EldenRingLauncher` project in the Solution Explorer and select **Publish**.
4. You can use the existing publish profile or create a new Folder Profile targeting `bin\Release\net10.0-windows\win-x64\publish\`.
5. Click **Publish**.

