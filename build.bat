@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion
set "ROOT=%~dp0"

echo.
echo ========================================
echo   Building NetMind Project
echo ========================================
echo.

echo [1/3] Building Frontend...
cd /d "%ROOT%src\NetMind.Frontend"
if errorlevel 1 goto error

call npm install
if errorlevel 1 goto error

call npm run build
if errorlevel 1 goto error

cd /d "%ROOT%"
if errorlevel 1 goto error

echo.
echo [2/3] Publishing Backend...
dotnet publish "src\NetMind.WebApi\NetMind.WebApi.csproj" -c Release -r win-x64 --self-contained true -o "publish\netmind"
if errorlevel 1 goto error

echo.
echo [3/3] Assembling Frontend...
if not exist "publish\NetMind.Frontend\dist" mkdir "publish\NetMind.Frontend\dist"

xcopy "src\NetMind.Frontend\dist\*" "publish\NetMind.Frontend\dist\" /E /Y /I /Q
if errorlevel 1 goto error

echo.
echo ========================================
echo   Build Completed Successfully!
echo ========================================
echo.
echo Backend:  publish\netmind
echo Frontend: publish\NetMind.Frontend\dist
echo.
pause
goto :eof

:error
echo.
echo ========================================
echo   Build Failed!
echo ========================================
echo.
pause
exit /b 1
