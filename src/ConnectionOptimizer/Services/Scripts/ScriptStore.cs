using System.IO;
using System.Reflection;
using System.Text;

namespace ConnectionOptimizer.Services.Scripts;

/// <summary>
/// Holds the scripts, decrypted in memory, from the encrypted blob embedded in the executable.
/// There is no Scripts folder on disk. A script is written to a temporary file only for the moment it runs
/// (cmd.exe needs a file), and that file is deleted right after. The bytes written are byte-for-byte the
/// originals, so the scripts are never altered.
/// </summary>
public sealed class ScriptStore
{
    private const string ArchiveResource = "ConnectionOptimizer.ProtectedScripts.bin";
    private const string KeyResource = "ConnectionOptimizer.ScriptKey.txt";

    private readonly IReadOnlyDictionary<string, byte[]> _scripts;

    private ScriptStore(IReadOnlyDictionary<string, byte[]> scripts) => _scripts = scripts;

    public static ScriptStore Load()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        byte[] key = Convert.FromBase64String(ReadTextResource(assembly, KeyResource).Trim());
        byte[] blob = ReadBinaryResource(assembly, ArchiveResource);
        return new ScriptStore(ScriptArchive.Unpack(blob, key));
    }

    public bool Exists(string scriptFile) => _scripts.ContainsKey(scriptFile);

    /// <summary>
    /// Writes the script to a fresh temporary file and returns a handle that deletes it on dispose.
    /// </summary>
    public MaterializedScript Materialize(string scriptFile)
    {
        if (!_scripts.TryGetValue(scriptFile, out byte[]? data))
        {
            throw new FileNotFoundException($"Script '{scriptFile}' is not in the archive.");
        }

        string directory = Path.Combine(Path.GetTempPath(), "GS9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, scriptFile);
        File.WriteAllBytes(path, data);
        return new MaterializedScript(directory, path);
    }

    private static string ReadTextResource(Assembly assembly, string name) =>
        Encoding.UTF8.GetString(ReadBinaryResource(assembly, name));

    private static byte[] ReadBinaryResource(Assembly assembly, string name)
    {
        using Stream stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing from the build.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

/// <summary>A script written to a temp file for one run. Dispose removes the file and its folder.</summary>
public sealed class MaterializedScript(string directory, string path) : IDisposable
{
    public string Path { get; } = path;
    public string WorkingDirectory { get; } = directory;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(WorkingDirectory))
            {
                Directory.Delete(WorkingDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort. The OS clears %TEMP% eventually.
        }
    }
}
