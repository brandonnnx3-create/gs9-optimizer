using System.Windows;
using ConnectionOptimizer.ViewModels;

namespace ConnectionOptimizer.Views;

public sealed class UiService(Window owner) : IUiService
{
    public bool Confirm(ConfirmRequest request) =>
        new ConfirmDialog(request) { Owner = owner }.ShowDialog() == true;

    public void Shutdown() => Application.Current.Shutdown();
}
