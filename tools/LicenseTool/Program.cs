using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ConnectionOptimizer.Services.Licensing;

// G.S.9 access tool — only for the owner of the private key.
//
//   keygen <private-key.pem>
//       Create a key pair. Print the public key for LicensePublicKey.cs (then rebuild the app).
//
//   allow add    <private-key.pem> <HWID> "<name>" [YYYY-MM-DD]
//   allow remove <private-key.pem> <HWID>
//   allow list   <private-key.pem>
//       Edit the allowlist. These update allowlist.json (kept by you) and write allowlist.signed,
//       which you upload to your gist (its raw URL is baked into the app). Removing a HWID and
//       re-uploading revokes that PC on its next check (or within the grace period if it stays offline).
//
// Files live in the current folder: allowlist.json (plain, yours to keep) and allowlist.signed (upload this).

const string WorkingFile = "allowlist.json";
const string SignedFile = "allowlist.signed";

try
{
    return args switch
    {
        ["keygen", var keyFile] => KeyGen(keyFile),
        ["allow", "add", var keyFile, var hwid, var name] => Add(keyFile, hwid, name, expires: null),
        ["allow", "add", var keyFile, var hwid, var name, var expires] => Add(keyFile, hwid, name, expires),
        ["allow", "remove", var keyFile, var hwid] => Remove(keyFile, hwid),
        ["allow", "list", var keyFile] => List(keyFile),
        _ => Usage(),
    };
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static int KeyGen(string keyFile)
{
    if (File.Exists(keyFile))
    {
        Console.Error.WriteLine($"'{keyFile}' already exists. Refusing to overwrite a private key.");
        return 1;
    }

    using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(keyFile, key.ExportPkcs8PrivateKeyPem());

    Console.WriteLine($"Private key written to {Path.GetFullPath(keyFile)}");
    Console.WriteLine("Keep it private: anyone with this file can grant access. Never commit or share it.");
    Console.WriteLine();
    Console.WriteLine("Public key (paste into src/ConnectionOptimizer/Services/Licensing/LicensePublicKey.cs and rebuild):");
    Console.WriteLine(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    return 0;
}

static int Add(string keyFile, string hwidInput, string name, string? expires)
{
    string? hwid = HardwareIdCode.Normalize(hwidInput);
    if (hwid is null)
    {
        Console.Error.WriteLine($"'{hwidInput}' is not a valid hardware ID (expected XXXX-XXXX-XXXX-XXXX).");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(name))
    {
        Console.Error.WriteLine("The name cannot be empty.");
        return 1;
    }

    if (expires is not null && !AllowlistFormat.TryParseExpiry(expires, out _))
    {
        Console.Error.WriteLine($"'{expires}' is not a valid date (expected YYYY-MM-DD).");
        return 1;
    }

    List<AllowlistEntry> entries = LoadEntries();
    entries.RemoveAll(e => string.Equals(e.Hwid, hwid, StringComparison.OrdinalIgnoreCase));
    entries.Add(new AllowlistEntry { Hwid = hwid, Name = name.Trim(), Expires = expires });

    Write(keyFile, entries);
    Console.WriteLine($"Added {name.Trim()} ({hwid}){(expires is null ? string.Empty : $", expires {expires}")}.");
    return 0;
}

static int Remove(string keyFile, string hwidInput)
{
    string hwid = HardwareIdCode.Normalize(hwidInput) ?? hwidInput.Trim().ToUpperInvariant();
    List<AllowlistEntry> entries = LoadEntries();
    int removed = entries.RemoveAll(e => string.Equals(e.Hwid, hwid, StringComparison.OrdinalIgnoreCase));
    if (removed == 0)
    {
        Console.Error.WriteLine($"{hwid} is not in the allowlist.");
        return 1;
    }

    Write(keyFile, entries);
    Console.WriteLine($"Removed {hwid}. Upload {SignedFile}; that PC is revoked on its next check.");
    return 0;
}

static int List(string keyFile)
{
    List<AllowlistEntry> entries = LoadEntries();
    if (entries.Count == 0)
    {
        Console.WriteLine("The allowlist is empty.");
        return 0;
    }

    Console.WriteLine($"{entries.Count} authorized PC(s):");
    foreach (AllowlistEntry e in entries)
    {
        Console.WriteLine($"  {e.Hwid}   {e.Name}{(e.Expires is null ? string.Empty : $"   (expires {e.Expires})")}");
    }

    return 0;
}

static List<AllowlistEntry> LoadEntries()
{
    if (!File.Exists(WorkingFile))
    {
        return [];
    }

    Allowlist? current = JsonSerializer.Deserialize<Allowlist>(
        File.ReadAllText(WorkingFile), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    return current?.Entries.ToList() ?? [];
}

static void Write(string keyFile, List<AllowlistEntry> entries)
{
    var allowlist = new Allowlist
    {
        Version = 1,
        Updated = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        Entries = entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(),
    };

    File.WriteAllText(WorkingFile, JsonSerializer.Serialize(
        allowlist, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

    using ECDsa key = ECDsa.Create();
    key.ImportFromPem(File.ReadAllText(keyFile));
    File.WriteAllText(SignedFile, AllowlistFormat.Sign(allowlist, key));

    Console.WriteLine($"Wrote {WorkingFile} and {SignedFile}. Upload {SignedFile} to your gist.");
}

static int Usage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  LicenseTool keygen <private-key.pem>");
    Console.WriteLine("  LicenseTool allow add    <private-key.pem> <HWID> \"<name>\" [YYYY-MM-DD]");
    Console.WriteLine("  LicenseTool allow remove <private-key.pem> <HWID>");
    Console.WriteLine("  LicenseTool allow list   <private-key.pem>");
    return 1;
}
