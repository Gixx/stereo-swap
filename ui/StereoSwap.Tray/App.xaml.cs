using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using StereoSwap.Tray.ViewModels;
using Application = System.Windows.Application;

namespace StereoSwap.Tray;

public partial class App : Application
{
    private NotifyIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _vm = new MainViewModel();
        _window = new MainWindow(_vm);

        _tray = new NotifyIcon
        {
            Text = "StereoSwap",
            Visible = true,
            Icon = SystemIcons.Application
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowMain());
        menu.Items.Add("Test Left", null, (_, _) => _vm.TestLeftCommand.Execute(null));
        menu.Items.Add("Test Right", null, (_, _) => _vm.TestRightCommand.Execute(null));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _tray.Visible = false;
            Shutdown();
        });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ShowMain();

        // First run: show window; later starts can stay tray-only if desired.
        ShowMain();
    }

    private void ShowMain()
    {
        if (_window is null)
            return;

        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }
}
