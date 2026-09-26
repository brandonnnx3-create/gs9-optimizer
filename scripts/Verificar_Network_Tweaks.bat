@echo off
setlocal EnableExtensions

:: =========================================================================
:: Verificar_Network_Tweaks.bat
:: Solo LEE el registro: no modifica nada.
:: Comprueba los 10 valores de Network_Tweaks.cmd. Los 4 valores de interfaz
:: se comprueban en la interfaz de red activa AHORA (misma deteccion que el
:: script que aplica).
:: Codigo de salida: 0 = los 10 aplicados, 1 = falta alguno o es distinto.
:: =========================================================================

set /a OK=0, DIFF=0, MISSING=0

fltmc >nul 2>&1 || echo [AVISO] Sin permisos de administrador: si alguna clave no se puede leer, aparecera como FALTA.
echo.

:: -------------------------------------------------------------------------
:: TCP/IP interface specific tweaks (interfaz activa)
:: -------------------------------------------------------------------------
echo [Interfaz activa]
set "GUID="
for /f "delims=" %%G in ('powershell -NoProfile -Command "$i = (Find-NetRoute -RemoteIPAddress 1.1.1.1 -ErrorAction Stop)[0].InterfaceIndex; (Get-NetAdapter -InterfaceIndex $i -IncludeHidden -ErrorAction Stop).InterfaceGuid" 2^>nul') do set "GUID=%%G"
if not defined GUID goto :no_guid

echo   GUID: %GUID%
set "IFKEY=HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\%GUID%"
call :check "%IFKEY%" TcpAckFrequency           0x00000001
call :check "%IFKEY%" TCPNoDelay                0x00000001
call :check "%IFKEY%" TcpDelAckTicks            0x00000000
call :check "%IFKEY%" TcpMaxDataRetransmissions 0x00000002
goto :global

:no_guid
echo   [ERROR] No se detecto la interfaz de red activa: no se pueden verificar sus 4 valores.
set /a MISSING+=4

:global
:: -------------------------------------------------------------------------
:: Global TCP/IP Tweaks
:: -------------------------------------------------------------------------
echo.
echo [Global TCP/IP]
set "TCP=HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"
call :check "%TCP%" TcpNoDelay      0x00000001
call :check "%TCP%" TcpAckFrequency 0x00000001
call :check "%TCP%" TcpDelAckTicks  0x00000000
call :check "%TCP%" MaxUserPort     0x0000fffe

:: -------------------------------------------------------------------------
:: Network Throttling Index for Gaming
:: -------------------------------------------------------------------------
echo.
echo [Multimedia SystemProfile]
set "MM=HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
call :check "%MM%" NetworkThrottlingIndex 0xffffffff
call :check "%MM%" SystemResponsiveness   0x00000000

:: -------------------------------------------------------------------------
:: Resumen
:: -------------------------------------------------------------------------
echo.
echo Resultado: %OK% OK, %DIFF% distintos, %MISSING% faltantes (de 10).
if %OK%==10 (
    echo ESTADO: APLICADO
    pause
    exit /b 0
)
if %OK%==0 (echo ESTADO: NO APLICADO) else (echo ESTADO: APLICADO PARCIALMENTE)
pause
exit /b 1

:: -------------------------------------------------------------------------
:: :check  "clave"  nombre  valor_esperado
:: Compara numericamente, asi 0x1 y 0x00000001 cuentan como iguales.
:: -------------------------------------------------------------------------
:check
set "VTYPE="
set "VAL="
for /f "tokens=1,2,3" %%A in ('reg query "%~1" /v %~2 2^>nul') do if /i "%%A"=="%~2" (set "VTYPE=%%B" & set "VAL=%%C")
if not defined VAL (
    echo   [FALTA]    %~2   ^(esperado %~3^)
    set /a MISSING+=1
    goto :eof
)
if /i not "%VTYPE%"=="REG_DWORD" goto :check_diff
set "VN="
set "EN="
set /a VN=%VAL% 2>nul
set /a EN=%~3
if not defined VN goto :check_diff
if "%VN%"=="%EN%" (
    echo   [OK]       %~2 = %VAL%
    set /a OK+=1
    goto :eof
)
:check_diff
echo   [DISTINTO] %~2 = %VAL% %VTYPE%   ^(esperado %~3^)
set /a DIFF+=1
goto :eof
