# nanoFramework Build Script for DiSEqC Controller (PowerShell)
# Builds firmware using Docker Compose V2
# Uses: docker compose (not docker-compose)

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host "nanoFramework DiSEqC Controller Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Run Docker build
Write-Host "`nStarting Docker build container..." -ForegroundColor Yellow

docker compose run --build --rm nanoframework-build /work/toolchain/build.sh

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n========================================"  -ForegroundColor Green
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "`nFirmware location: build/nanoCLR.bin" -ForegroundColor Green
    Write-Host "`nTo flash to board:" -ForegroundColor Yellow
    Write-Host "  st-flash write build/nanoCLR.bin 0x08000000" -ForegroundColor White
} else {
    Write-Host "`n========================================"  -ForegroundColor Red
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    exit 1
}
