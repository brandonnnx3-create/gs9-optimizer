using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ConnectionOptimizer.Services.Licensing;

/// <summary>
/// This PC's hardware ID: motherboard UUID + motherboard serial (SMBIOS) + Windows MachineGuid.
/// It stays the same across app updates and user accounts; it changes if Windows is reinstalled
/// or the motherboard is replaced.
/// </summary>
public static class HardwareId
{
    private const uint RawSmbiosProvider = 0x52534D42; // 'RSMB'

    public static string Get()
    {
        (string uuid, string boardSerial) = ReadSmbios();
        string machineGuid = ReadMachineGuid();
        if (uuid.Length == 0 && boardSerial.Length == 0 && machineGuid.Length == 0)
        {
            throw new InvalidOperationException("No hardware identifiers could be read on this PC.");
        }

        return HardwareIdCode.FromComponents(uuid, boardSerial, machineGuid);
    }

    private static (string SystemUuid, string BoardSerial) ReadSmbios()
    {
        uint size = GetSystemFirmwareTable(RawSmbiosProvider, 0, null, 0);
        if (size == 0)
        {
            return (string.Empty, string.Empty);
        }

        var buffer = new byte[size];
        return GetSystemFirmwareTable(RawSmbiosProvider, 0, buffer, size) == size
            ? SmbiosParser.Parse(buffer)
            : (string.Empty, string.Empty);
    }

    private static string ReadMachineGuid()
    {
        using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return (key?.GetValue("MachineGuid") as string)?.Trim() ?? string.Empty;
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetSystemFirmwareTable(uint provider, uint tableId, byte[]? buffer, uint bufferSize);
}
