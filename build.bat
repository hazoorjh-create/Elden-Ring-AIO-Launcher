@echo off
echo Installing requirements...
pip install -r requirements.txt

echo.
echo Building eldenring.exe...
pyinstaller --noconfirm --onedir --windowed --name "eldenring" --add-data "%VIRTUAL_ENV%\Lib\site-packages\customtkinter;customtkinter/" "launcher.py"

echo.
echo NOTE: Since PyInstaller onefile with customtkinter can be slow to start and tricky with paths,
echo we are using onedir. However, to make it a standalone .exe, we can also try onefile:
echo Building standalone onefile version as well...
pyinstaller --noconfirm --onefile --windowed --name "eldenring-standalone" --add-data "%VIRTUAL_ENV%\Lib\site-packages\customtkinter;customtkinter/" "launcher.py"

echo.
echo Build complete! Check the "dist" folder.
pause
