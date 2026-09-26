@echo off
echo ==========================================
echo       Network Latency Optimizer
echo ==========================================
echo.

:: Request Administrator privileges if not already running as admin
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run this script as Administrator!
    pause
    exit
)

echo [1/4] Applying System Timer Tweaks...
bcdedit /set disabledynamictick yes >nul
bcdedit /deletevalue useplatformclock >nul 2>&1
bcdedit /set useplatformtick yes >nul

echo [2/4] Optimizing Network Adapter Power Settings...
powershell -Command "Disable-NetAdapterPowerManagement -Name '*' -ErrorAction SilentlyContinue"

echo [3/4] Optimizing Realtek Network Adapter Advanced Settings...
:: Disables Power saving features that cause latency spikes
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Energy-Efficient Ethernet' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Green Ethernet' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Advanced EEE' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Power Saving Mode' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Auto Disable Gigabit' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"

:: Disables Flow Control and Interrupt Moderation to reduce packet buffering (lower latency at cost of CPU)
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Flow Control' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Interrupt Moderation' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"

:: Disables Offloading (ARP, NS, Large Send, Checksum) - Stops the network card from processing extra logic
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'ARP Offload' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'NS Offload' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Large Send Offload v2 (IPv4)' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Large Send Offload v2 (IPv6)' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Disable-NetAdapterChecksumOffload -Name '*' -TcpIPv4 -UdpIPv4 -IpIPv4 -ErrorAction SilentlyContinue"

:: Disables Wake on LAN features (Prevents the card from staying active waiting for boot signals)
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Wake on Magic Packet' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Wake on pattern match' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"
powershell -Command "Set-NetAdapterAdvancedProperty -Name '*' -DisplayName 'Shutdown Wake-On-Lan' -DisplayValue 'Disabled' -ErrorAction SilentlyContinue"

:: Set interface metric to 1 to prioritize the active network interface
powershell -Command "Set-NetIPInterface -InterfaceAlias '*' -InterfaceMetric 1 -ErrorAction SilentlyContinue"

echo [4/4] Applying TCP/IP Global Settings...
netsh int tcp set global dca=enabled >nul
netsh int tcp set global netdma=enabled >nul
netsh int tcp set global timestamps=disabled >nul
netsh int tcp set global nonsackrttresiliency=disabled >nul
netsh int tcp set global initialRto=2000 >nul

echo.
echo Optimization Complete! 
echo Please restart your computer to fully apply the changes.
pause