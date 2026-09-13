@echo off
REM Safe deploy to C:\icongrid — only copies DLLs and EXEs, NEVER data files
echo Stopping IconGrid...
taskkill /f /im IconGrid.exe >nul 2>&1
taskkill /f /im IconGridFpsAgent.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo Copying runtime files...
set SRC=E:\IconGrid-GitHub\bin\Debug\net10.0-windows10.0.22621.0
set DST=C:\icongrid

REM Only copy DLLs, EXEs, and supporting runtime files — skip data/config files
xcopy /y /q "%SRC%\*.dll" "%DST%\" >nul
xcopy /y /q "%SRC%\*.exe" "%DST%\" >nul
xcopy /y /q "%SRC%\*.runtimeconfig.json" "%DST%\" >nul
xcopy /y /q "%SRC%\*.deps.json" "%DST%\" >nul

REM The native FPS agent lives in a SUBFOLDER. xcopy without /s does NOT recurse,
REM so "%SRC%\*.exe" never covered it and the deployed agent silently stayed
REM stale (last shipped build was 2026-07-23). Copy it explicitly.
xcopy /y /q /i "%SRC%\Tools\FpsAgent\IconGridFpsAgent.exe" "%DST%\Tools\FpsAgent\" >nul

echo Done. IconGrid deployed to C:\icongrid
echo Start: C:\icongrid\IconGrid.exe