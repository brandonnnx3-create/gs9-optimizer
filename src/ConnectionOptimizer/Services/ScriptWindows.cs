using System.Runtime.InteropServices;
using System.Text;

namespace ConnectionOptimizer.Services;

/// <summary>A visible window opened by a running script (for example the Disk Cleanup settings).</summary>
public sealed record ScriptWindow(IntPtr Handle, string Title, string ProcessName);

/// <summary>
/// Finds windows that belong to the process tree of a running script, so the app can tell the user
/// the script is waiting on a window instead of looking frozen.
/// </summary>
public static class ScriptWindows
{
    private const uint SnapProcess = 0x00000002;
    private const int RestoreWindow = 9;
    private static readonly IntPtr InvalidHandle = new(-1);

    public static IReadOnlyList<ScriptWindow> Find(int rootProcessId)
    {
        Dictionary<int, string> tree = GetProcessTree(rootProcessId);
        var windows = new List<ScriptWindow>();

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out uint processId);
            if (!tree.TryGetValue((int)processId, out string? processName))
            {
                return true;
            }

            int length = GetWindowTextLength(handle);
            if (length == 0)
            {
                return true; // Untitled helper windows are not something the user can act on.
            }

            var title = new StringBuilder(length + 1);
            GetWindowText(handle, title, title.Capacity);
            windows.Add(new ScriptWindow(handle, title.ToString(), processName));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public static void BringToFront(IntPtr handle)
    {
        if (IsIconic(handle))
        {
            ShowWindow(handle, RestoreWindow);
        }

        SetForegroundWindow(handle);
    }

    /// <summary>The root process and all its descendants, with their executable names.</summary>
    private static Dictionary<int, string> GetProcessTree(int rootProcessId)
    {
        var names = new Dictionary<int, string>();
        var children = new Dictionary<int, List<int>>();

        IntPtr snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);
        if (snapshot == InvalidHandle)
        {
            return new Dictionary<int, string>();
        }

        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            for (bool ok = Process32First(snapshot, ref entry); ok; ok = Process32Next(snapshot, ref entry))
            {
                int id = (int)entry.ProcessId;
                int parent = (int)entry.ParentProcessId;
                names[id] = entry.ExeFile;
                if (!children.TryGetValue(parent, out List<int>? list))
                {
                    children[parent] = list = [];
                }

                list.Add(id);
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var tree = new Dictionary<int, string>();
        var pending = new Queue<int>([rootProcessId]);
        while (pending.Count > 0)
        {
            int id = pending.Dequeue();
            if (!tree.TryAdd(id, names.GetValueOrDefault(id, "?")))
            {
                continue;
            }

            if (children.TryGetValue(id, out List<int>? list))
            {
                foreach (int child in list)
                {
                    pending.Enqueue(child);
                }
            }
        }

        return tree;
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int BasePriority;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode)]
    private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode)]
    private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);
}
