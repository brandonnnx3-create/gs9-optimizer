using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ConnectionOptimizer.Views;

/// <summary>
/// Keeps the native Windows title bar (snap, resize and accessibility for free) but paints it black.
/// The color attributes exist on Windows 11; on Windows 10 only the dark mode applies.
/// </summary>
internal static class WindowTheming
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    // COLORREF is 0x00BBGGRR.
    private const int Black = 0x00050505;
    private const int Line = 0x00242424;
    private const int White = 0x00FFFFFF;

    public static void UseDarkChrome(Window window) =>
        window.SourceInitialized += (_, _) => Apply(new WindowInteropHelper(window).Handle);

    private static void Apply(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        Set(handle, UseImmersiveDarkMode, 1);
        Set(handle, CaptionColor, Black);
        Set(handle, BorderColor, Line);
        Set(handle, TextColor, White);
    }

    // Failures are expected on older Windows builds and leave the default frame.
    private static void Set(IntPtr handle, int attribute, int value) =>
        _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
