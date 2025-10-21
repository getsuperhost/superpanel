#!/bin/bash

echo "Building SuperPanel Web Host Control Panel..."

# Build Native Library
echo "Building C++ Native Library..."
if command -v msbuild &> /dev/null; then
    msbuild src/NativeLibrary/SuperPanel.NativeLibrary.vcxproj /p:Configuration=Release /p:Platform=x64
else
    echo "MSBuild not found. Please build the Native Library manually in Visual Studio."
fi

# Build Web API
echo "Building Web API..."
cd src/WebAPI || exit 1
dotnet restore
dotnet build --configuration Release
cd ../.. || exit 1

# Build Desktop App
echo "Building Desktop Application..."
cd src/DesktopApp || exit 1
dotnet restore
dotnet build --configuration Release
cd ../.. || exit 1

# Build Web UI
echo "Building Web UI..."
cd src/WebUI || exit 1
if command -v npm &> /dev/null; then
    npm install
    npm run build
else
    echo "npm not found. Please install Node.js and npm."
fi
cd ../.. || exit 1

echo "Build completed!"
echo ""
echo "To run the applications:"
echo "1. Web API: cd src/WebAPI && dotnet run"
echo "2. Desktop App: cd src/DesktopApp && dotnet run"
echo "3. Web UI (dev): cd src/WebUI && npm run dev"