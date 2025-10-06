# ShiftManager - First-Time Setup Script
# This script sets up a fresh development environment

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ShiftManager - First-Time Setup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if seed.db exists
if (-Not (Test-Path "seed.db")) {
    Write-Host "ERROR: seed.db not found!" -ForegroundColor Red
    Write-Host "Please make sure you're in the project root directory." -ForegroundColor Yellow
    exit 1
}

# Copy seed database to app.db
Write-Host "[1/3] Copying seed database to app.db..." -ForegroundColor Green
Copy-Item -Path "seed.db" -Destination "app.db" -Force
Write-Host "      ✓ Database copied successfully" -ForegroundColor Gray
Write-Host ""

# Restore NuGet packages
Write-Host "[2/3] Restoring NuGet packages..." -ForegroundColor Green
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore packages" -ForegroundColor Red
    exit 1
}
Write-Host "      ✓ Packages restored" -ForegroundColor Gray
Write-Host ""

# Build the project
Write-Host "[3/3] Building the project..." -ForegroundColor Green
dotnet build
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "      ✓ Build successful" -ForegroundColor Gray
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "You can now run the application with:" -ForegroundColor White
Write-Host "  dotnet run" -ForegroundColor Yellow
Write-Host ""
Write-Host "Default login credentials (Development):" -ForegroundColor White
Write-Host "  Email:    admin@local" -ForegroundColor Yellow
Write-Host "  Password: admin123" -ForegroundColor Yellow
Write-Host ""
Write-Host "Application will be available at:" -ForegroundColor White
Write-Host "  http://localhost:5000" -ForegroundColor Yellow
Write-Host "  https://localhost:5001" -ForegroundColor Yellow
Write-Host ""
