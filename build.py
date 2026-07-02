import os
import subprocess
import shutil

def build():
    print("Installing requirements...")
    subprocess.check_call(["pip", "install", "-r", "requirements.txt"])
    
    print("Installing Nuitka (A safer compiler that avoids false virus flags)...")
    subprocess.check_call(["pip", "install", "nuitka"])

    # Ensure output directory exists
    if not os.path.exists("output"):
        os.makedirs("output")

    # Convert icon if present
    icon_arg = ""
    if os.path.exists("icon.png"):
        try:
            print("Converting icon.png to icon.ico...")
            from PIL import Image
            img = Image.open("icon.png")
            img.save("icon.ico", format="ICO", sizes=[(256, 256)])
            icon_arg = "--windows-icon-from-ico=icon.ico"
        except Exception as e:
            print("Failed to convert icon:", e)
    elif os.path.exists("icon.ico"):
        icon_arg = "--windows-icon-from-ico=icon.ico"
        
    print("\nBuilding EldenRingAIOLauncher.exe using Nuitka...")
    print("This might take a few minutes as it compiles Python to C to ensure it's completely safe.")
    
    import customtkinter
    ctk_path = os.path.dirname(customtkinter.__file__)
    
    cmd = [
        "python", "-m", "nuitka",
        "--onefile",
        "--standalone",
        "--onefile-no-compression",
        "--windows-console-mode=disable",
        "--assume-yes-for-downloads",
        "--enable-plugin=tk-inter",
        "--output-filename=EldenRingAIOLauncher.exe",
        "--windows-company-name=Created by Dante69K",
        "--windows-product-name=Elden Ring AIO Launcher",
        "--windows-file-version=1.0.0.0",
        "--windows-product-version=1.0.0.0",
        "--windows-file-description=Elden Ring Custom Launcher",
        "--output-dir=output",
        f"--include-data-dir={ctk_path}=customtkinter",
    ]
    
    if icon_arg:
        cmd.append(icon_arg)
        
    cmd.append("launcher.py")
    
    subprocess.check_call(cmd)
    
    print("\nBuild complete! You will find your executable inside the 'output' directory.")

if __name__ == "__main__":
    build()
