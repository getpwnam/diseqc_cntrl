# Build wrapper for PowerShell → WSL
# Since Docker runs in WSL, this script bridges the gap

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "DiSEqC Build (PowerShell → WSL)"  -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`nInvoking build in WSL..." -ForegroundColor Yellow

# Run build in WSL
wsl bash -c "cd /home/cp/Dev/diseqc_cntrl/software/nanoFramework && chmod +x build.sh && docker compose run --rm nanoframework-build /work/build.sh"

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n========================================"  -ForegroundColor Green
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "`nFirmware location: build/nanoCLR.bin" -ForegroundColor Green
} else {
    Write-Host "`n========================================"  -ForegroundColor Red
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}
