using System.Diagnostics;
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
        // Keep minimize-to-tray in normal use; under the debugger exit instead so F5 rebuilds.
        if (WindowState == WindowState.Minimized && !Debugger.IsAttached)
            Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (Debugger.IsAttached)
        {
            // Allow real shutdown so the DLL is unlocked for the next F5 build.
            base.OnClosing(e);
            return;
        }

        // Close-to-tray: hide instead of exit (exit via tray menu).
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
