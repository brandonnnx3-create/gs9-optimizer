using System.Security.Cryptography;
using ConnectionOptimizer.Services.Scripts;

// Build step: encrypts the scripts in <scriptsDir> into <outFile>, using the key in <keyFile>
// (created with a fresh random key if it does not exist). Run by the app's build; not shipped.
//
//   ScriptPacker <scriptsDir> <keyFile> <outFile>

if (args is not [var scriptsDir, var keyFile, var outFile])
{
    Console.Error.WriteLine("Usage: ScriptPacker <scriptsDir> <keyFile> <outFile>");
    return 1;
}

byte[] key = LoadOrCreateKey(keyFile);

var scripts = new SortedDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
foreach (string path in Directory.EnumerateFiles(scriptsDir)
             .Where(p => p.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
                      || p.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)))
{
    scripts[Path.GetFileName(path)] = File.ReadAllBytes(path); // byte-for-byte, scripts are never altered
}

if (scripts.Count == 0)
{
    Console.Error.WriteLine($"No .bat or .cmd files found in {scriptsDir}.");
    return 1;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
File.WriteAllBytes(outFile, ScriptArchive.Pack(scripts, key));
Console.WriteLine($"Packed {scripts.Count} scripts -> {outFile}");
return 0;

static byte[] LoadOrCreateKey(string keyFile)
{
    if (File.Exists(keyFile))
    {
        return Convert.FromBase64String(File.ReadAllText(keyFile).Trim());
    }

    byte[] key = RandomNumberGenerator.GetBytes(32);
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(keyFile))!);
    File.WriteAllText(keyFile, Convert.ToBase64String(key));
    return key;
}
