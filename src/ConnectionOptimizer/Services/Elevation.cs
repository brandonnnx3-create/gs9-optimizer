using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;

namespace ConnectionOptimizer.Services;

public static class Elevation
{
    private const int ErrorCancelled = 1223;

    public static bool IsElevated { get; } = CheckElevated();

    /// <summary>
    /// Starts a new instance of the app through UAC. Returns false if the user declined the prompt.
    /// </summary>
    public static bool TryRestartElevated(string arguments)
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The executable path is not available.");

        var psi = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory,
        };

        try
        {
            Process.Start(psi)?.Dispose();
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    private static bool CheckElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
