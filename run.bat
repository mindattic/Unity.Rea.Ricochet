@echo off
rem Asks first: 1) Deploy & Run pulls latest, always rebuilds, publishes to C:\Apps\Unity.Rea.Ricochet,
rem refreshes the Desktop shortcut and launches it (run.ps1 -Force -Launch does the work);
rem 2) Run just launches the copy already in C:\Apps\Unity.Rea.Ricochet.
setlocal
set "EXE=C:\Apps\Unity.Rea.Ricochet\Ricochet.exe"
echo.
echo   Unity.Rea.Ricochet
echo     1^) Deploy ^& Run
echo     2^) Run
echo.
choice /c 12 /n /m "  Choose 1 or 2: "
if errorlevel 2 goto run

:deploy
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" -Force -Launch %*
if errorlevel 1 (
    echo.
    echo   Deploy failed. See Logs\deploy.log.
    if exist "%EXE%" (
        echo   Launching the installed copy instead.
        start "" /d "C:\Apps\Unity.Rea.Ricochet" "%EXE%"
    )
    pause
    exit /b 1
)
timeout /t 5
exit /b 0

:run
if not exist "%EXE%" (
    echo   Nothing is deployed to C:\Apps\Unity.Rea.Ricochet yet; deploying first.
    goto deploy
)
start "" /d "C:\Apps\Unity.Rea.Ricochet" "%EXE%" %*
