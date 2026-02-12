# Git Commit Script for DiSEqC Controller
# Commits all relevant files (excludes build artifacts)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DiSEqC Controller - Git Commit Helper" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check if we're in a git repository
if (!(Test-Path .git)) {
    Write-Host "ERROR: Not in a git repository!" -ForegroundColor Red
    Write-Host "Run: git init" -ForegroundColor Yellow
    exit 1
}

Write-Host "`nAdding files to git..." -ForegroundColor Yellow

# Build system files
Write-Host "  - Build system files" -ForegroundColor Green
git add docker-compose.yml
git add build.sh
git add build.ps1
git add build/CMakeLists.txt
git add build/mcuconf.h
git add .gitignore

# Documentation
Write-Host "  - Documentation" -ForegroundColor Green
git add README.md
git add QUICK_START.md
git add PROJECT_COMPLETE_SUMMARY.md
git add BUILD_SYSTEM_COMPLETE.md
git add DOCS_REORGANIZED.md
git add docs/

# Native code
Write-Host "  - Native code (C++)" -ForegroundColor Green
git add nf-native/*.h
git add nf-native/*.cpp

# C# application
Write-Host "  - C# application" -ForegroundColor Green
git add DiseqC/*.cs
git add DiseqC/*.csproj
git add DiseqC/packages.config
git add DiseqC/Properties/
git add DiseqC/Native/
git add DiseqC/Manager/

# Show status
Write-Host "`nFiles staged for commit:" -ForegroundColor Yellow
git status --short

Write-Host "`nReady to commit!" -ForegroundColor Green
Write-Host "Suggested commit message:" -ForegroundColor Yellow
Write-Host @"

feat: Complete DiSEqC controller with Docker build system

Application Features:
- DiSEqC 1.2 rotor control (GotoAngle, Step, Drive, Halt)
- LNB control via I2C (13V/18V voltage, 22kHz tone)
- W5500 Ethernet connectivity
- MQTT remote control (16 commands, 12 status topics)
- Home Assistant integration ready

Native Drivers:
- diseqc_native.cpp: 22kHz PWM carrier + GPT bit timing
- lnb_control.cpp: I2C control for LNBH26PQR
- C# interop layers for both drivers

Build System:
- Docker-based build (no local toolchain)
- CMake configuration for STM32F407VG
- MCU peripheral configuration
- PowerShell and Bash build scripts

Hardware Support:
- STM32F407VGT6 MCU
- LNBH26PQR LNB controller (I2C)
- W5500 Ethernet (SPI)
- 8MHz HSE crystal

Documentation:
- Complete testing guide
- MQTT API reference
- Architecture overview
- Docker build guide
- All guides organized in docs/

Ready to flash and control satellite dishes! 🛰️

"@ -ForegroundColor Cyan

Write-Host "`nTo commit, run:" -ForegroundColor Yellow
Write-Host '  git commit -F commit_message.txt' -ForegroundColor White
Write-Host "Or copy the message above" -ForegroundColor Gray

# Optionally create commit message file
$commitMsg = @"
feat: Complete DiSEqC controller with Docker build system

Application Features:
- DiSEqC 1.2 rotor control (GotoAngle, Step, Drive, Halt)
- LNB control via I2C (13V/18V voltage, 22kHz tone)
- W5500 Ethernet connectivity
- MQTT remote control (16 commands, 12 status topics)
- Home Assistant integration ready

Native Drivers:
- diseqc_native.cpp: 22kHz PWM carrier + GPT bit timing
- lnb_control.cpp: I2C control for LNBH26PQR
- C# interop layers for both drivers

Build System:
- Docker-based build (no local toolchain)
- CMake configuration for STM32F407VG
- MCU peripheral configuration
- PowerShell and Bash build scripts

Hardware Support:
- STM32F407VGT6 MCU
- LNBH26PQR LNB controller (I2C)
- W5500 Ethernet (SPI)
- 8MHz HSE crystal

Documentation:
- Complete testing guide
- MQTT API reference
- Architecture overview
- Docker build guide
- All guides organized in docs/

Ready to flash and control satellite dishes! 🛰️
"@

$commitMsg | Out-File -FilePath commit_message.txt -Encoding UTF8

Write-Host "`nCommit message saved to: commit_message.txt" -ForegroundColor Green
