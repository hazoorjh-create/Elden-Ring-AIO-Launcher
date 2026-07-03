$pfxPath = "Dante69K.pfx"
$password = "eldenring"
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe"

if (-not (Test-Path $signtool)) {
    # Try to find another signtool if the hardcoded one doesn't exist
    $signtool = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match "x64" } | Select-Object -First 1 -ExpandProperty FullName
    
    if (-not $signtool) {
        Write-Host "Signtool not found. Please install Windows SDK or find signtool.exe."
        exit 1
    }
}

$filesToSign = @(
    "bin\Release\net10.0-windows\win-x64\EldenRingAIOLauncher.dll",
    "bin\Release\net10.0-windows\win-x64\EldenRingAIOLauncher.exe",
    "bin\Release\net10.0-windows\win-x64\publish_unpacked\EldenRingAIOLauncher.dll",
    "bin\Release\net10.0-windows\win-x64\publish_unpacked\EldenRingAIOLauncher.exe",
    "bin\Release\net10.0-windows\win-x64\publish\EldenRingAIOLauncher.exe"
)

Write-Host "Using signtool at: $signtool"

foreach ($file in $filesToSign) {
    if (Test-Path $file) {
        & $signtool sign /f $pfxPath /p $password /fd SHA256 /t "http://timestamp.digicert.com" $file
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully signed: $file"
        } else {
            Write-Host "Failed to sign: $file"
        }
    } else {
        Write-Host "File not found (ensure you have published the project): $file"
    }
}
