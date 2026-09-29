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
        Status = DescribeApoGate() + " " + Status;
    }

    private string DescribeToneStatus(bool logicalLeft)
    {
        var side = logicalLeft ? "Left" : "Right";
        if (!SwapEnabled)
            return $"{side} tone on that WAV channel (swap off). If your speakers are wired swapped, you will hear it on the opposite side.";

        if (ApoStatus.IsComRegistered())
            return $"{side} tone — system APO should correct physical side when swap is ON.";

        return $"{side} tone with in-app swap preview ON (system APO not installed yet — only these test buttons are swapped, not Spotify/YouTube).";
    }

    private static string DescribeApoGate()
    {
        return ApoStatus.IsComRegistered()
            ? "System APO registered."
            : "Note: system-wide L↔R needs StereoSwapApo.dll (not installed). Swap checkbox still previews on Left/Right test buttons.";
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

        if (!ApoStatus.IsComRegistered())
        {
            Status = enable
                ? "Swap ON for test tones (in-app). System audio unchanged until StereoSwapApo.dll is built & registered."
                : "Swap OFF. Test tones play on their true WAV channels again.";
            return;
        }

        // Config.json is enough for the enable flag once APO is bound; bind/unbind for first-time MVP.
        if (_apo.TryBindDevice(SelectedDevice.Id, enable, out var msg))
            Status = msg;
        else
            Status = msg + " Config saved.";
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
