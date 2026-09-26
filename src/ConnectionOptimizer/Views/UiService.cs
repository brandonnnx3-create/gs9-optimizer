using System.Diagnostics;
using System.IO;
using System.Windows;
using ConnectionOptimizer.ViewModels;

namespace ConnectionOptimizer.Views;

public sealed class UiService(Window owner) : IUiService
{
    public bool Confirm(ConfirmRequest request) =>
        new ConfirmDialog(request) { Owner = owner }.ShowDialog() == true;

    public void ShowLog(LogView log) => new LogWindow(log) { Owner = owner }.Show();

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public void Shutdown() => Application.Current.Shutdown();
}
