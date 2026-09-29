using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using StereoSwap.Tray.Models;
using StereoSwap.Tray.Services;
using Application = System.Windows.Application;

namespace StereoSwap.Tray.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SettingsStore _settingsStore = new();
    private readonly AudioDeviceService _devices = new();
    private readonly AutostartService _autostart = new();
    private readonly TestToneService _tone = new();
    private readonly ApoInstallClient _apo = new();

    private AppSettings _settings = new();
    private AudioRenderDevice? _selectedDevice;
    private bool _swapEnabled;
    private bool _autostartEnabled;
    private string _status = "Ready.";

    public ObservableCollection<AudioRenderDevice> Devices { get; } = [];

    public AudioRenderDevice? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (Set(ref _selectedDevice, value))
                Persist();
        }
    }

    public bool SwapEnabled
    {
        get => _swapEnabled;
        set
        {
            if (!Set(ref _swapEnabled, value))
                return;
            ApplySwap(value);
        }
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set
        {
            if (!Set(ref _autostartEnabled, value))
                return;
            _autostart.SetEnabled(value);
            Persist();
        }
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand TestLeftCommand { get; }
    public ICommand TestRightCommand { get; }
    public ICommand ExitCommand { get; }

    public MainViewModel()
    {
        RefreshCommand = new RelayCommand(RefreshDevices);
        TestLeftCommand = new RelayCommand(() =>
        {
            _tone.PlayLeftOnly(SwapEnabled);
            Status = DescribeToneStatus(logicalLeft: true);
        });
        TestRightCommand = new RelayCommand(() =>
        {
            _tone.PlayRightOnly(SwapEnabled);
            Status = DescribeToneStatus(logicalLeft: false);
        });
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

        _settings = _settingsStore.Load();
        _swapEnabled = _settings.SwapEnabled;
        _autostartEnabled = _settings.Autostart || _autostart.IsEnabled();
        RefreshDevices();
        Status = DescribeApoGate();
    }

    private string DescribeToneStatus(bool logicalLeft)
    {
        var side = logicalLeft ? "Left" : "Right";
        if (!SwapEnabled)
            return $"{side} tone (swap off). If cabling is reversed, you hear it on the opposite speaker.";

        if (ApoStatus.IsComRegistered())
            return $"{side} tone — system APO should place it on the correct physical side.";

        return $"{side} tone with in-app preview only. Run Install-Apo.bat (admin) for system-wide swap.";
    }

    private static string DescribeApoGate()
    {
        if (ApoStatus.IsMemoryIntegrityEnabled())
            return "Blocked: Memory Integrity (Core Isolation) is ON — unsigned APO cannot load. Turn it OFF in Windows Security, reboot, then enable swap.";

        if (!ApoStatus.IsComRegistered())
            return "System APO not installed yet. Build apo\\Build-Apo.bat, then packaging\\Install-Apo.bat (UAC).";

        if (!ApoStatus.IsProtectedAudioDgDisabled())
            return "APO registered, but DisableProtectedAudioDG is not set. Enable swap once (UAC), then reboot.";

        return "System APO ready — select Optical device and enable L↔R swap.";
    }

    public void RefreshDevices()
    {
        Devices.Clear();
        foreach (var d in _devices.GetRenderDevices().Where(d => !d.IsExcluded))
            Devices.Add(d);

        SelectedDevice = Devices.FirstOrDefault(d => d.Id == _settings.SelectedDeviceId)
                         ?? Devices.FirstOrDefault(d => d.IsDefault)
                         ?? Devices.FirstOrDefault();

        Status = $"{Devices.Count} output device(s) (Beacn excluded).";
    }

    private void ApplySwap(bool enable)
    {
        Persist();

        if (SelectedDevice is null)
        {
            Status = "No device selected.";
            return;
        }

        Status = enable ? "Requesting admin to enable system APO..." : "Requesting admin to disable system APO...";

        if (_apo.TrySetSwap(SelectedDevice.Id, enable, out var msg))
        {
            Status = msg;
            return;
        }

        Status = msg;
    }

    private void Persist()
    {
        _settings.SelectedDeviceId = SelectedDevice?.Id;
        _settings.SwapEnabled = _swapEnabled;
        _settings.Autostart = _autostartEnabled;
        _settingsStore.Save(_settings);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

public sealed class RelayCommand(Action execute) : ICommand
{
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
