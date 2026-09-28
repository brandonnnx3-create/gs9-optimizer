using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ConnectionOptimizer.Services.Licensing;

/// <summary>
/// Turns hardware identifiers into a short code like <c>7K2Q-9MXD-4TRB-P0VH</c>
/// (Crockford base32: no I, L, O or U, so it is easy to read out and type).
/// </summary>
public static partial class HardwareIdCode
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string FromComponents(string systemUuid, string boardSerial, string machineGuid)
    {
        string material = $"gs9|{systemUuid}|{boardSerial}|{machineGuid}".ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Encode(hash.AsSpan(0, 10));
    }

    /// <summary>Uppercases and trims user input; returns null when it is not a valid code.</summary>
    public static string? Normalize(string? code)
    {
        string value = (code ?? string.Empty).Trim().ToUpperInvariant();
        return CodePattern().IsMatch(value) ? value : null;
    }

    /// <summary>10 bytes → 16 base32 characters in 4 groups.</summary>
    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder(19);
        int buffer = 0;
        int bits = 0;
        foreach (byte b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                AppendChar((buffer >> bits) & 31);
            }
        }

        return text.ToString();

        void AppendChar(int index)
        {
            if (text.Length is 4 or 9 or 14)
            {
                text.Append('-');
            }

            text.Append(Alphabet[index]);
        }
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){3}$")]
    private static partial Regex CodePattern();
}

/// <summary>Reads the fields the hardware ID uses from a raw SMBIOS table (as returned by GetSystemFirmwareTable 'RSMB').</summary>
public static class SmbiosParser
{
    private const int HeaderSize = 8;

    public static (string SystemUuid, string BoardSerial) Parse(byte[] raw)
    {
        string uuid = string.Empty;
        string boardSerial = string.Empty;
        if (raw.Length < HeaderSize)
        {
            return (uuid, boardSerial);
        }

        int end = Math.Min(raw.Length, HeaderSize + BitConverter.ToInt32(raw, 4));
        int offset = HeaderSize;
        while (offset + 4 <= end)
        {
            byte type = raw[offset];
            byte length = raw[offset + 1];
            if (length < 4 || offset + length > end)
            {
                break;
            }

            // The formatted area is followed by its strings, ended by a double NUL.
            int stringsStart = offset + length;
            int stringsEnd = stringsStart;
            while (stringsEnd + 1 < end && !(raw[stringsEnd] == 0 && raw[stringsEnd + 1] == 0))
            {
                stringsEnd++;
            }

            string[] strings = stringsEnd > stringsStart
                ? Encoding.Latin1.GetString(raw, stringsStart, stringsEnd - stringsStart).Split('\0')
                : [];

            if (type == 1 && length >= 0x18)
            {
                uuid = Convert.ToHexString(raw, offset + 8, 16);
            }
            else if (type == 2 && length >= 0x08)
            {
                boardSerial = StringAt(strings, raw[offset + 7]);
            }
            else if (type == 127)
            {
                break;
            }

            offset = stringsEnd + 2;
        }

        return (uuid, boardSerial);
    }

    private static string StringAt(string[] strings, byte index) =>
        index >= 1 && index <= strings.Length ? strings[index - 1].Trim() : string.Empty;
}
