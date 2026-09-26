using System.Windows;
using ConnectionOptimizer.ViewModels;

namespace ConnectionOptimizer.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(ConfirmRequest request)
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        DataContext = request;
    }

    // Cancel is handled by IsCancel (button and Esc).
    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
