using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ConnectionOptimizer.Services.Scripts;

/// <summary>
/// Bundles the scripts into a single AES-256-GCM encrypted blob so they are not shipped as loose files.
/// Same code is used to pack (tools/ScriptPacker) and to unpack (the app), with the key from build/script-key.txt.
///
/// This keeps the scripts out of the install folder and out of plain sight. It is not unbreakable: the key
/// travels inside the executable, so it raises the effort to read the scripts, it does not make it impossible.
/// </summary>
public static class ScriptArchive
{
    private const uint Magic = 0x47533943; // "GS9C"
    private const int NonceSize = 12;
    private const int TagSize = 16;

    /// <summary>Container layout (before encryption): [count][ (nameLen,name,dataLen,data) x count ].</summary>
    public static byte[] Pack(IReadOnlyDictionary<string, byte[]> scripts, byte[] key)
    {
        using var plain = new MemoryStream();
        WriteInt(plain, scripts.Count);
        foreach ((string name, byte[] data) in scripts)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            WriteInt(plain, nameBytes.Length);
            plain.Write(nameBytes);
            WriteInt(plain, data.Length);
            plain.Write(data);
        }

        byte[] plaintext = plain.ToArray();
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        using var output = new MemoryStream();
        WriteUInt(output, Magic);
        output.Write(nonce);
        output.Write(tag);
        output.Write(ciphertext);
        return output.ToArray();
    }

    public static IReadOnlyDictionary<string, byte[]> Unpack(byte[] blob, byte[] key)
    {
        if (blob.Length < 4 + NonceSize + TagSize || BinaryPrimitives.ReadUInt32LittleEndian(blob) != Magic)
        {
            throw new InvalidDataException("The script archive is not in the expected format.");
        }

        var nonce = blob.AsSpan(4, NonceSize);
        var tag = blob.AsSpan(4 + NonceSize, TagSize);
        var ciphertext = blob.AsSpan(4 + NonceSize + TagSize);
        byte[] plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, TagSize))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        var reader = new SpanReader(plaintext);
        int count = reader.ReadInt();
        var scripts = new Dictionary<string, byte[]>(count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < count; i++)
        {
            string name = Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt()));
            scripts[name] = reader.ReadBytes(reader.ReadInt());
        }

        return scripts;
    }

    private static void WriteInt(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private ref struct SpanReader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;
        private int _position;

        public int ReadInt()
        {
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }

        public byte[] ReadBytes(int length)
        {
            byte[] result = _data.Slice(_position, length).ToArray();
            _position += length;
            return result;
        }
    }
}
