using System.Globalization;
using System.Security.Cryptography;
using ConnectionOptimizer.Services.Licensing;

// G.S.9 license tool — only for the owner of the private key.
//
//   LicenseTool keygen <private-key.pem>
//       Creates a new key pair. Prints the public key to paste into
//       src/ConnectionOptimizer/Services/Licensing/LicensePublicKey.cs (then rebuild the app).
//
//   LicenseTool issue <private-key.pem> <HWID> "<name>" [license.key]
//       Creates a license for one PC. The HWID is shown on the app's lock screen.

return args switch
{
    ["keygen", var keyFile] => KeyGen(keyFile),
    ["issue", var keyFile, var hwid, var name] => Issue(keyFile, hwid, name, "license.key"),
    ["issue", var keyFile, var hwid, var name, var output] => Issue(keyFile, hwid, name, output),
    _ => Usage(),
};

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
    Console.WriteLine("Keep it private: anyone with this file can create licenses. Never commit it.");
    Console.WriteLine();
    Console.WriteLine("Public key (paste into LicensePublicKey.cs and rebuild the app):");
    Console.WriteLine(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    return 0;
}

static int Issue(string keyFile, string hwidInput, string name, string output)
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

    using ECDsa key = ECDsa.Create();
    key.ImportFromPem(File.ReadAllText(keyFile));

    var payload = new LicensePayload
    {
        Hwid = hwid,
        Name = name.Trim(),
        Issued = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    File.WriteAllText(output, LicenseFormat.Create(payload, key));
    Console.WriteLine($"License for {payload.Name} ({payload.Hwid}) written to {Path.GetFullPath(output)}");
    return 0;
}

static int Usage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  LicenseTool keygen <private-key.pem>");
    Console.WriteLine("  LicenseTool issue <private-key.pem> <HWID> \"<name>\" [license.key]");
    return 1;
}
