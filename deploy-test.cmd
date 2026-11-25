@echo off
REM Safe deploy to C:\icongrid — only copies DLLs and EXEs, NEVER data files
echo Stopping IconGrid...

REM The elevated monitor agent may have been started by the "IconGrid Monitor"
REM scheduled task (no launcher parent). It then keeps the DLLs loaded until its
REM "no launcher process present" grace period (60s) expires, which made xcopy
REM fail with "Sharing violation" and the new build was silently not deployed.
REM Ask it to stop through the well-known request-stop event first.
powershell -NoProfile -Command "try { [System.Threading.EventWaitHandle]::OpenExisting('Local\IconGrid.HardwareMonitorAgent.RequestStop').Set() } catch { }; exit 0" >nul 2>&1
timeout /t 3 /nobreak >nul

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

REM Runtime-specific assemblies are resolved through deps.json and MUST be
REM copied, otherwise the app fails with FileNotFoundException. Examples:
REM System.ServiceProcess.ServiceController under runtimes\win\lib\net8.0
REM and LibreHardwareMonitorLib under runtimes\win-x64\lib\net10.0.
if exist "%SRC%\runtimes" xcopy /y /q /e /i "%SRC%\runtimes" "%DST%\runtimes" >nul

REM The native FPS agent lives in a SUBFOLDER. xcopy without /s does NOT recurse,
REM so "%SRC%\*.exe" never covered it and the deployed agent silently stayed
REM stale (last shipped build was 2026-07-23). Copy it explicitly.
xcopy /y /q /i "%SRC%\Tools\FpsAgent\IconGridFpsAgent.exe" "%DST%\Tools\FpsAgent\" >nul

REM Static UI assets that are still content files loaded with pack://siteoforigin
REM MUST exist next to the EXE. They are assets, not user data/config, so they
REM are safe to overwrite on every deploy. Hardware vendor logos are embedded as
REM WPF resources in IconGrid.dll and are intentionally NOT part of this copy.
if exist "%SRC%\Assets" xcopy /y /q /e /i "%SRC%\Assets" "%DST%\Assets" >nul
if exist "%SRC%\figma-icons" xcopy /y /q /i "%SRC%\figma-icons\*" "%DST%\figma-icons\" >nul

echo Done. IconGrid deployed to C:\icongrid
echo Start: C:\icongrid\IconGrid.exe