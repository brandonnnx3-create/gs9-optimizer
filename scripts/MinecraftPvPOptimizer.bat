@echo off
color 0A
title Optimizador de Red para Minecraft (Hit Registration)

:: Comprobar permisos de administrador
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo =======================================================
    echo ERROR: Permisos de Administrador requeridos.
    echo =======================================================
    echo Por favor, haz click derecho en este archivo y selecciona
    echo "Ejecutar como administrador" para aplicar los cambios.
    echo.
    pause
    exit
)

echo =======================================================
echo Aplicando Optimizaciones de Red para Minecraft PvP
echo =======================================================
echo.

echo 1. Desactivando Network Throttling (evita picos de lag en combates)...
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" /v "NetworkThrottlingIndex" /t REG_DWORD /d 4294967295 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" /v "SystemResponsiveness" /t REG_DWORD /d 0 /f >nul 2>&1

echo 2. Priorizando los juegos en el sistema...
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" /v "GPU Priority" /t REG_DWORD /d 8 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" /v "Priority" /t REG_DWORD /d 6 /f >nul 2>&1
reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" /v "Scheduling Category" /t REG_SZ /d "High" /f >nul 2>&1

echo 3. Aplicando TcpNoDelay y TcpAckFrequency (Cero Delay en hits)...
for /f "delims=" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\services\Tcpip\Parameters\Interfaces"') do (
    reg add "%%a" /v "TcpAckFrequency" /t REG_DWORD /d 1 /f >nul 2>&1
    reg add "%%a" /v "TCPNoDelay" /t REG_DWORD /d 1 /f >nul 2>&1
    reg add "%%a" /v "TcpDelAckTicks" /t REG_DWORD /d 0 /f >nul 2>&1
)

:: MSMQ (Microsoft Message Queue) settings for overall latency
reg add "HKLM\SOFTWARE\Microsoft\MSMQ\Parameters" /v "TCPNoDelay" /t REG_DWORD /d 1 /f >nul 2>&1

echo.
echo =======================================================
echo Optimizaciones aplicadas exitosamente.
echo =======================================================
echo Es recomendable reiniciar tu computadora para que los
echo cambios en la placa de red hagan efecto.
echo.
pause