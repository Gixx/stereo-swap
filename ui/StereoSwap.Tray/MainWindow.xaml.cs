using System.Windows;
using StereoSwap.Tray.ViewModels;

namespace StereoSwap.Tray;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
            Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Close-to-tray: hide instead of exit (exit via tray menu).
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
