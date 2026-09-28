using System.Diagnostics;
using ConnectionOptimizer.Services.Licensing;

// G.S.9 HWID detector: shows this PC's hardware ID (the same one Connection Optimizer uses)
// and copies it to the clipboard. It reads the ID only: it changes nothing and sends nothing.

Console.Title = "G.S.9 — HWID";
Console.WriteLine();
Console.WriteLine("  G.S.9  -  HARDWARE ID");
Console.WriteLine("  -------------------------------------");

try
{
    string hwid = HardwareId.Get();
    Console.WriteLine();
    Console.WriteLine($"  {hwid}");
    Console.WriteLine();
    Console.WriteLine(TryCopyToClipboard(hwid)
        ? "  Copied to the clipboard. Send it to G.S.9."
        : "  Send this ID to G.S.9.");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"  The hardware ID could not be read: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("  Press Enter to close.");
Console.ReadLine();

static bool TryCopyToClipboard(string text)
{
    try
    {
        using var clip = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "clip.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
        });
        if (clip is null)
        {
            return false;
        }

        clip.StandardInput.Write(text);
        clip.StandardInput.Close();
        return clip.WaitForExit(3000) && clip.ExitCode == 0;
    }
    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
    {
        return false;
    }
}
