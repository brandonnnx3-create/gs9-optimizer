@echo off
title Limpieza y Optimizacion Anti Input Lag
color 0A

echo ================================
echo Limpieza de archivos temporales
echo ================================
del /q/f/s %TEMP%\*
del /q/f/s C:\Windows\Temp\*
rd /s /q C:\$Recycle.Bin

echo ================================
echo Limpieza de cache DNS
echo ================================
ipconfig /flushdns

echo ================================
echo Reparando archivos del sistema
echo ================================
sfc /scannow
DISM /Online /Cleanup-Image /RestoreHealth

echo ================================
echo Limpiando cache Microsoft Store
echo ================================
wsreset.exe


echo ================================
echo Deteniendo servicios de telemetria
echo ================================
sc stop "dmwappushservice"

echo ================================
echo Limpiando cola de impresion
echo ================================
net stop spooler
del /Q /F /S "%systemroot%\System32\spool\PRINTERS\*.*"
net start spooler

echo ================================
echo Optimizando disco
echo ================================
defrag C: /O

echo ================================
echo Limpieza profunda de Windows
echo ================================
cleanmgr /sageset:1
cleanmgr /sagerun:1

echo.
echo ===== LISTO! Reinicia tu PC =====
pause
