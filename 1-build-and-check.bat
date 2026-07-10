@echo off
setlocal enabledelayedexpansion

rem dps-nn - BUILD AND CHECK ONLY. Does not generate anything.
rem Double-click this first; once it's green, use 2-generate.bat to actually
rem run the tool. Safe to re-run any time - it never touches out/, logs/,
rem prompts/, or seeds/.
cd /d "%~dp0"

echo ============================================================
echo   dps-nn - build and check
echo ============================================================
echo.

echo [1/4] Checking for the .NET SDK...
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo.
    echo ERROR: no "dotnet" found on PATH.
    echo Install the .NET SDK first: https://dotnet.microsoft.com/download
    echo ^(this project targets net10.0 - see global.json^)
    echo.
    pause
    exit /b 1
)
for /f "delims=" %%v in ('dotnet --version') do echo       found .NET SDK %%v
echo.

if not exist dps-nn.sln if not exist dps-nn.slnx (
    echo [2/4] First run: creating the solution and adding the 3 projects...
    dotnet new sln -n dps-nn
    if errorlevel 1 goto :error
    dotnet sln add src\Genesis.Core\Genesis.Core.csproj
    if errorlevel 1 goto :error
    dotnet sln add src\Genesis.Cli\Genesis.Cli.csproj
    if errorlevel 1 goto :error
    dotnet sln add tests\Genesis.Tests\Genesis.Tests.csproj
    if errorlevel 1 goto :error
) else (
    echo [2/4] Solution already exists, skipping.
)
echo.

echo [3/4] Restoring and building...
dotnet build
if errorlevel 1 goto :error
echo.

echo [4/4] Running tests...
dotnet test
if errorlevel 1 goto :error
echo.

echo ============================================================
echo   All green. Run 2-generate.bat next to actually generate a project.
echo ============================================================
echo.
pause
exit /b 0

:error
echo.
echo ============================================================
echo   Something failed above - scroll up to see the real error.
echo ============================================================
echo.
pause
exit /b 1
