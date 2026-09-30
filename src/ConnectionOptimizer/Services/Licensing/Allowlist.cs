using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConnectionOptimizer.Services.Licensing;

/// <summary>One authorized PC in the allowlist.</summary>
public sealed record AllowlistEntry
{
    public required string Hwid { get; init; }
    public required string Name { get; init; }

    /// <summary>Optional expiry, "yyyy-MM-dd". Null means no expiry.</summary>
    public string? Expires { get; init; }
}

/// <summary>The signed list of PCs allowed to run the app. The owner edits it and re-signs with the private key.</summary>
public sealed record Allowlist
{
    public int Version { get; init; } = 1;
    public string Updated { get; init; } = string.Empty;
    public IReadOnlyList<AllowlistEntry> Entries { get; init; } = [];

    public AllowlistEntry? Find(string hwid) =>
        Entries.FirstOrDefault(e => string.Equals(e.Hwid, hwid, StringComparison.OrdinalIgnoreCase));
}

public enum AllowlistDecision
{
    Allowed,
    NotListed,
    Expired,
}

/// <summary>
/// Signs and verifies the allowlist, and decides whether a hardware ID may run.
/// Format (like the license): header line, then base64url(json).base64url(ECDSA P-256/SHA-256 signature).
/// Only the private-key holder can produce a list the app will trust.
/// </summary>
public static class AllowlistFormat
{
    public const string Header = "GS9-ALLOWLIST-1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static string Sign(Allowlist allowlist, ECDsa privateKey)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(allowlist, Json);
        byte[] signature = privateKey.SignData(data, HashAlgorithmName.SHA256);
        return $"{Header}\n{Base64Url.EncodeToString(data)}.{Base64Url.EncodeToString(signature)}\n";
    }

    /// <summary>Returns the allowlist when the text is signed with the matching private key; otherwise null.</summary>
    public static Allowlist? Verify(string text, ECDsa publicKey)
    {
        string[] lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 2 || lines[0] != Header)
        {
            return null;
        }

        string[] parts = lines[1].Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        try
        {
            byte[] data = Base64Url.DecodeFromChars(parts[0]);
            byte[] signature = Base64Url.DecodeFromChars(parts[1]);
            if (!publicKey.VerifyData(data, signature, HashAlgorithmName.SHA256))
            {
                return null;
            }

            Allowlist? allowlist = JsonSerializer.Deserialize<Allowlist>(data, Json);
            return allowlist is { Version: 1 } ? allowlist : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        {
            return null;
        }
    }

    public static (AllowlistDecision Decision, AllowlistEntry? Entry) Evaluate(Allowlist allowlist, string hwid, DateTime now)
    {
        AllowlistEntry? entry = allowlist.Find(hwid);
        if (entry is null)
        {
            return (AllowlistDecision.NotListed, null);
        }

        if (TryParseExpiry(entry.Expires, out DateTime expiry) && now.Date > expiry.Date)
        {
            return (AllowlistDecision.Expired, entry);
        }

        return (AllowlistDecision.Allowed, entry);
    }

    public static bool TryParseExpiry(string? value, out DateTime expiry)
    {
        expiry = default;
        return !string.IsNullOrWhiteSpace(value)
            && DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out expiry);
    }
}
