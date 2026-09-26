@echo off
setlocal EnableExtensions

:: =========================================================================
:: Network_Tweaks.cmd
:: Mismos valores que Network_Tweaks.reg, pero aplicados a la interfaz de
:: red activa de esta PC en lugar de un GUID fijo.
:: Ejecutar con clic derecho > "Ejecutar como administrador".
:: =========================================================================

:: Requiere administrador (escribe en HKLM)
fltmc >nul 2>&1 || (echo [ERROR] Ejecutar como administrador.& pause & exit /b 1)

:: -------------------------------------------------------------------------
:: TCP/IP interface specific tweaks (interfaz activa detectada en esta PC)
:: Interfaz activa = la que Windows usa para salir a Internet.
:: Find-NetRoute solo consulta la tabla de rutas: no envia trafico.
:: -------------------------------------------------------------------------
set "GUID="
for /f "delims=" %%G in ('powershell -NoProfile -Command "$i = (Find-NetRoute -RemoteIPAddress 1.1.1.1 -ErrorAction Stop)[0].InterfaceIndex; (Get-NetAdapter -InterfaceIndex $i -IncludeHidden -ErrorAction Stop).InterfaceGuid" 2^>nul') do set "GUID=%%G"
if not defined GUID (echo [ERROR] No se detecto la interfaz de red activa.& pause & exit /b 1)

set "IFKEY=HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\%GUID%"
reg query "%IFKEY%" >nul 2>&1 || (echo [ERROR] No existe %IFKEY%& pause & exit /b 1)

reg add "%IFKEY%" /v TcpAckFrequency           /t REG_DWORD /d 0x00000001 /f >nul || goto :fail
reg add "%IFKEY%" /v TCPNoDelay                /t REG_DWORD /d 0x00000001 /f >nul || goto :fail
reg add "%IFKEY%" /v TcpDelAckTicks            /t REG_DWORD /d 0x00000000 /f >nul || goto :fail
reg add "%IFKEY%" /v TcpMaxDataRetransmissions /t REG_DWORD /d 0x00000002 /f >nul || goto :fail

:: -------------------------------------------------------------------------
:: Global TCP/IP Tweaks
:: -------------------------------------------------------------------------
set "TCP=HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
reg add "%TCP%" /v TcpNoDelay      /t REG_DWORD /d 0x00000001 /f >nul || goto :fail
reg add "%TCP%" /v TcpAckFrequency /t REG_DWORD /d 0x00000001 /f >nul || goto :fail
reg add "%TCP%" /v TcpDelAckTicks  /t REG_DWORD /d 0x00000000 /f >nul || goto :fail
reg add "%TCP%" /v MaxUserPort     /t REG_DWORD /d 0x0000fffe /f >nul || goto :fail

:: -------------------------------------------------------------------------
:: Network Throttling Index for Gaming
:: Disables Windows' default behavior of throttling network traffic
:: when multimedia apps are running.
:: -------------------------------------------------------------------------
set "MM=HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
reg add "%MM%" /v NetworkThrottlingIndex /t REG_DWORD /d 0xffffffff /f >nul || goto :fail
reg add "%MM%" /v SystemResponsiveness   /t REG_DWORD /d 0x00000000 /f >nul || goto :fail

echo [OK] Valores aplicados. Interfaz: %GUID%
echo Reiniciar la PC para que los cambios tengan efecto.
pause
exit /b 0

:fail
echo [ERROR] Fallo al escribir en el registro.
pause
exit /b 1
