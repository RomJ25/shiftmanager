#!/bin/bash
# ShiftManager - First-Time Setup Script
# This script sets up a fresh development environment

echo "========================================"
echo "ShiftManager - First-Time Setup"
echo "========================================"
echo ""

# Check if seed.db exists
if [ ! -f "seed.db" ]; then
    echo "ERROR: seed.db not found!"
    echo "Please make sure you're in the project root directory."
    exit 1
fi

# Copy seed database to app.db
echo "[1/3] Copying seed database to app.db..."
cp seed.db app.db
echo "      ✓ Database copied successfully"
echo ""

# Restore NuGet packages
echo "[2/3] Restoring NuGet packages..."
dotnet restore
if [ $? -ne 0 ]; then
    echo "ERROR: Failed to restore packages"
    exit 1
fi
echo "      ✓ Packages restored"
echo ""

# Build the project
echo "[3/3] Building the project..."
dotnet build
if [ $? -ne 0 ]; then
    echo "ERROR: Build failed"
    exit 1
fi
echo "      ✓ Build successful"
echo ""

echo "========================================"
echo "Setup Complete!"
echo "========================================"
echo ""
echo "You can now run the application with:"
echo "  dotnet run"
echo ""
echo "Default login credentials (Development):"
echo "  Email:    admin@local"
echo "  Password: admin123"
echo ""
echo "Application will be available at:"
echo "  http://localhost:5000"
echo "  https://localhost:5001"
echo ""
