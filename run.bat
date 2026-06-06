@echo off
cd /d "%~dp0"

if "%SV_PORT%"=="" (set PORT=25000) else (set PORT=%SV_PORT%)
if "%SV_NAME%"=="" (set NAME=Rivet Server) else (set NAME=%SV_NAME%)
if "%SV_MAXPLAYERS%"=="" (set MAX=8) else (set MAX=%SV_MAXPLAYERS%)
if "%SV_PASSWORD%"=="" (set PASS=) else (set PASS=-password "%SV_PASSWORD%")

echo Starting Rivet server on port %PORT%...
dotnet run --project src\Rivet.csproj -- -port %PORT% -servername "%NAME%" -maxplayers %MAX% %PASS%
