@echo off
echo Building SuperPanel Web Host Control Panel...

REM Build Native Library
echo Building C++ Native Library...
msbuild src\NativeLibrary\SuperPanel.NativeLibrary.vcxproj /p:Configuration=Release /p:Platform=x64
if %errorlevel% neq 0 (
    echo Failed to build Native Library
    pause
    exit /b %errorlevel%
)

REM Build Web API
echo Building Web API...
cd src\WebAPI
dotnet restore
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo Failed to build Web API
    cd ..\..
    pause
    exit /b %errorlevel%
)
cd ..\..

REM Build Desktop App
echo Building Desktop Application...
cd src\DesktopApp
dotnet restore
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo Failed to build Desktop App
    cd ..\..
    pause
    exit /b %errorlevel%
)
cd ..\..

REM Build Web UI
echo Building Web UI...
cd src\WebUI
npm install
if %errorlevel% neq 0 (
    echo Failed to install npm packages
    cd ..\..
    pause
    exit /b %errorlevel%
)
npm run build
if %errorlevel% neq 0 (
    echo Failed to build Web UI
    cd ..\..
    pause
    exit /b %errorlevel%
)
cd ..\..

echo Build completed successfully!
echo.
echo To run the applications:
echo 1. Web API: cd src\WebAPI ^& dotnet run
echo 2. Desktop App: cd src\DesktopApp ^& dotnet run
echo 3. Web UI (dev): cd src\WebUI ^& npm run dev
pause