@echo off
cd /d "%~dp0"

echo === Building Rivet ===
dotnet build src\Rivet.csproj -c Release %*

if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo === Publishing standalone binary ===
dotnet publish src\Rivet.csproj -c Release -o publish ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=embedded

if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo === Build complete ===
echo Binary: publish\Rivet.exe
echo Run: publish\Rivet.exe -port 25000 -servername "My Server" -maxplayers 8
