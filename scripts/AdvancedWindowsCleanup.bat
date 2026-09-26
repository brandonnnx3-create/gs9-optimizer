@echo off
title Limpieza avanzada Windows 10/11

echo Cerrando navegadores...
taskkill /f /im chrome.exe >nul 2>&1
taskkill /f /im msedge.exe >nul 2>&1
taskkill /f /im firefox.exe >nul 2>&1

echo Limpiando temporales del usuario...
del /f /s /q "%TEMP%*" >nul 2>&1
for /d %%D in ("%TEMP%*") do rd /s /q "%%D" >nul 2>&1

echo Limpiando temporales de Windows...
del /f /s /q "C:\Windows\Temp*" >nul 2>&1
for /d %%D in ("C:\Windows\Temp*") do rd /s /q "%%D" >nul 2>&1

echo Limpiando archivos recientes...
del /f /q "%APPDATA%\Microsoft\Windows\Recent*" >nul 2>&1

echo Limpiando cache de miniaturas...
del /f /q "%LOCALAPPDATA%\Microsoft\Windows\Explorer\thumbcache_*" >nul 2>&1

echo Limpiando cache DNS...
ipconfig /release
ipconfig /renew
ipconfig /flushdns

echo Limpiando cache de Google Chrome...
rd /s /q "%LOCALAPPDATA%\Google\Chrome\User Data\Default\Cache" >nul 2>&1
rd /s /q "%LOCALAPPDATA%\Google\Chrome\User Data\Default\Code Cache" >nul 2>&1

echo Limpiando cache de Microsoft Edge...
rd /s /q "%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Cache" >nul 2>&1
rd /s /q "%LOCALAPPDATA%\Microsoft\Edge\User Data\Default\Code Cache" >nul 2>&1

echo Limpiando cache de Firefox...
for /d %%D in ("%LOCALAPPDATA%\Mozilla\Firefox\Profiles*") do (
rd /s /q "%%D\cache2" >nul 2>&1
)

echo Vaciando papelera...
powershell -Command "Clear-RecycleBin -Force" >nul 2>&1

echo.
echo Limpieza finalizada.
pause