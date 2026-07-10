@echo off
setlocal enabledelayedexpansion

rem dps-nn - GENERATE. Run 1-build-and-check.bat first if you haven't yet.
rem Runs exactly ONE spec per invocation - if there's more than one
rem project-spec*.json in cwd/fixtures/, or more than one file under seeds/,
rem you'll get a numbered menu to pick from; run this script again to
rem generate a different one. Every fresh generation lands in its own
rem timestamped folder under out/ - re-running never overwrites a previous
rem generation of the same project.
cd /d "%~dp0"

if not exist dps-nn.sln if not exist dps-nn.slnx (
    echo.
    echo No dps-nn.sln/.slnx found yet - run 1-build-and-check.bat first.
    echo.
    pause
    exit /b 1
)

set MARKER=logs\.pending-outdir.txt

rem If a previous invocation is still waiting on a manually-pasted LLM
rem response (exit code 3), resume it in the SAME folder it started in -
rem otherwise we'd silently restart that project from scratch in a new one.
if exist "%MARKER%" (
    set /p OUTDIR=<"%MARKER%"
    echo ============================================================
    echo   dps-nn - resuming the pending run in: !OUTDIR!
    echo ============================================================
) else (
    for /f "delims=" %%i in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set TS=%%i
    set OUTDIR=out\run_!TS!
    echo ============================================================
    echo   dps-nn - starting a new run in: !OUTDIR!
    echo ============================================================
)
echo.

rem No --spec: if there's more than one candidate, you'll be asked to pick
rem ONE. No --out folder-per-project-name: !OUTDIR! keeps every run separate.
dotnet run --project src\Genesis.Cli -- run --all --out "!OUTDIR!" --resume
set PIPELINE_EXIT=!errorlevel!
echo.

if !PIPELINE_EXIT! equ 3 (
    if not exist logs mkdir logs >nul 2>&1
    echo !OUTDIR!> "%MARKER%"
    echo ------------------------------------------------------------
    echo Waiting for a manual LLM response - see the instructions above.
    echo Paste it where it says, then run this script again; it will
    echo resume in the SAME folder: !OUTDIR!
    echo ------------------------------------------------------------
    pause
    exit /b 3
)

if exist "%MARKER%" del "%MARKER%" >nul 2>&1

if !PIPELINE_EXIT! neq 0 goto :error

echo ============================================================
echo   Done. Project generated in: !OUTDIR!
echo   See the banner above for the URL and login credentials.
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
