using System.ComponentModel;
using System.Windows;
using ConnectionOptimizer.ViewModels;

namespace ConnectionOptimizer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        FitToWorkArea();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && !viewModel.ConfirmClose())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    private void FitToWorkArea()
    {
        Rect area = SystemParameters.WorkArea;
        Width = Math.Max(MinWidth, Math.Min(Width, area.Width - 48));
        Height = Math.Max(MinHeight, Math.Min(Height, area.Height - 48));
    }

    // Keep the newest activity entry in view.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel viewModel)
        {
            viewModel.Activity.Entries.CollectionChanged += (_, _) => ActivityScroll.ScrollToEnd();
        }
    }
}
