using System.IO;
using System.Security.Cryptography;

namespace ConnectionOptimizer.Services.Licensing;

public enum LicenseState
{
    Valid,
    Missing,
    Invalid,
}

public sealed record LicenseCheck(LicenseState State, string? Hwid, string? LicensedTo, string Message)
{
    public bool IsValid => State == LicenseState.Valid;
}

/// <summary>
/// Private access: the app only opens on PCs that have a license signed for their hardware ID.
/// The license is looked up in %LOCALAPPDATA%\GS9\ConnectionOptimizer\license.key, then next to the executable.
/// </summary>
public sealed class LicenseService(string dataDirectory)
{
    public const string FileName = "license.key";

    public string InstalledLicensePath { get; } = Path.Combine(dataDirectory, FileName);

    public LicenseCheck Check()
    {
        string hwid;
        try
        {
            hwid = HardwareId.Get();
        }
        catch (Exception ex)
        {
            return new LicenseCheck(LicenseState.Invalid, null, null, $"The hardware ID could not be read: {ex.Message}");
        }

        LicenseCheck? firstProblem = null;
        foreach (string path in new[] { InstalledLicensePath, Path.Combine(AppContext.BaseDirectory, FileName) })
        {
            if (!File.Exists(path))
            {
                continue;
            }

            LicenseCheck check = CheckFile(path, hwid);
            if (check.IsValid)
            {
                return check;
            }

            firstProblem ??= check;
        }

        return firstProblem ?? new LicenseCheck(LicenseState.Missing, hwid, null, "No license found on this PC.");
    }

    /// <summary>Validates a license file chosen by the user and installs it when it is valid for this PC.</summary>
    public LicenseCheck Import(string path)
    {
        LicenseCheck current = Check();
        if (current.Hwid is not { } hwid)
        {
            return current;
        }

        LicenseCheck check = CheckFile(path, hwid);
        if (!check.IsValid)
        {
            return check;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(InstalledLicensePath)!);
            File.Copy(path, InstalledLicensePath, overwrite: true);
            return check;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return check with { State = LicenseState.Invalid, Message = $"The license is valid but could not be saved: {ex.Message}" };
        }
    }

    private static LicenseCheck CheckFile(string path, string hwid)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LicenseCheck(LicenseState.Invalid, hwid, null, $"The license file could not be read: {ex.Message}");
        }

        using ECDsa publicKey = LicenseFormat.ImportPublicKey(LicensePublicKey.Value);
        LicensePayload? payload = LicenseFormat.Verify(text, publicKey);
        if (payload is null)
        {
            return new LicenseCheck(LicenseState.Invalid, hwid, null, "This is not a valid G.S.9 license (signature check failed).");
        }

        if (!string.Equals(payload.Hwid, hwid, StringComparison.OrdinalIgnoreCase))
        {
            return new LicenseCheck(LicenseState.Invalid, hwid, null, $"This license was issued for another PC ({payload.Hwid}).");
        }

        return new LicenseCheck(LicenseState.Valid, hwid, payload.Name, $"Licensed to {payload.Name}.");
    }
}
