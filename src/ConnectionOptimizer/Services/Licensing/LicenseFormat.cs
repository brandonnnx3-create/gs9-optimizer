using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConnectionOptimizer.Services.Licensing;

/// <summary>What a license grants: one hardware ID, with a name to recognize it.</summary>
public sealed record LicensePayload
{
    public int Version { get; init; } = 1;
    public required string Hwid { get; init; }
    public required string Name { get; init; }
    public required string Issued { get; init; }
}

/// <summary>
/// License file format, shared by the app (verify) and tools/LicenseTool (sign):
/// <code>
/// GS9-LICENSE-1
/// base64url(payload json).base64url(ECDSA P-256 / SHA-256 signature)
/// </code>
/// Only the holder of the private key can create a license; the app ships the public key only.
/// </summary>
public static class LicenseFormat
{
    public const string Header = "GS9-LICENSE-1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Create(LicensePayload payload, ECDsa privateKey)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
        byte[] signature = privateKey.SignData(data, HashAlgorithmName.SHA256);
        return $"{Header}\r\n{Base64Url.EncodeToString(data)}.{Base64Url.EncodeToString(signature)}\r\n";
    }

    /// <summary>Returns the payload when the text is a license signed with the matching private key; otherwise null.</summary>
    public static LicensePayload? Verify(string text, ECDsa publicKey)
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

            LicensePayload? payload = JsonSerializer.Deserialize<LicensePayload>(data, Json);
            return payload is { Version: 1 } ? payload : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or CryptographicException)
        {
            return null;
        }
    }

    public static ECDsa ImportPublicKey(string base64SubjectPublicKeyInfo)
    {
        var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(base64SubjectPublicKeyInfo), out _);
        return key;
    }
}
