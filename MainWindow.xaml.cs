using NAudio.CoreAudioApi;
using SharpDX.DirectInput;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ThrottleShaker.Audio;
using ThrottleShaker.Config;
using ThrottleShaker.Core;
using ThrottleShaker.Input;
using ThrottleShaker.Service;
using ThrottleShaker.Enum;

namespace ThrottleShaker;

public partial class MainWindow : Window
{
    private readonly AudioDeviceService _audioSvc = new();
    private readonly DirectInputService _inputSvc = new();

    private readonly EffectMixer _mixer = new();

    private RumbleEngine? _engine;
    private BindingEvaluator? _eval;
    private ConfigStoreService? _store;
    private MultiJoystickPoller? _poller;
    private GlobalInputLogger _logger;

    // Thread | Timers
    private readonly object _sync = new();
    private DispatcherTimer? _engineTimer;
    private long _lastTick = Environment.TickCount64;

    // Profile
    private ActiveProfile _profile = new();

    // Controller Event States
    private readonly Dictionary<(Guid dev, int btn), RumbleEffect> _activeHolds = new();

    // Device States
    private readonly Dictionary<Guid, JoystickState> _latestStateByDevice = new();
    private readonly Dictionary<Guid, bool[]> _prevButtonsByDevice = new();
    private readonly Dictionary<Guid, string> _deviceNamesByGuid = new();

    private readonly ObservableCollection<BindingRow> _bindingRows = new();
    private readonly List<BindingDefinition> _bindings = new();

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            _store = new ConfigStoreService();
            _logger = new GlobalInputLogger(DeviceName);

            BindingsGrid.ItemsSource = _bindingRows;

            StartBtn.Click += (_, _) => Start();
            StopBtn.Click += (_, _) => Stop();

            RefreshDevices();
            LoadActiveProfileAndApply();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startup error");
            throw;
        }
    }

    private void RefreshDevices()
    {
        var audio = _audioSvc.GetRenderDevices();
        AudioDevices.ItemsSource = audio;
        AudioDevices.DisplayMemberPath = "FriendlyName";
        if (AudioDevices.SelectedIndex < 0 && audio.Count > 0) AudioDevices.SelectedIndex = 0;

        var joys = _inputSvc.ListJoysticks();

        var items = joys
            .Select(d => new
            {
                Device = d,
                Label = $"{d.InstanceName}  |  {d.InstanceGuid}"
            })
            .ToList();

        ThrottleDevices.ItemsSource = items;
        ThrottleDevices.DisplayMemberPath = "Label";
        ThrottleDevices.SelectedValuePath = "Device";

        StickDevices.ItemsSource = items;
        StickDevices.DisplayMemberPath = "Label";
        StickDevices.SelectedValuePath = "Device";

        if (ThrottleDevices.SelectedIndex < 0 && ThrottleDevices.Items.Count > 0)
            ThrottleDevices.SelectedIndex = 0;

        if (StickDevices.SelectedIndex < 0 && StickDevices.Items.Count > 0)
            StickDevices.SelectedIndex = 0;
    }

    private void LoadActiveProfileAndApply()
    {
        _profile = LoadActiveProfile();
        ApplySavedSelections();
    }

    private void ApplySavedSelections()
    {
        if (_profile.AppConfig.SelectedAudioDeviceId is string aid)
        {
            var audio = (IEnumerable<MMDevice>)AudioDevices.ItemsSource;
            var match = audio.FirstOrDefault(x => x.ID == aid);
            if (match != null) AudioDevices.SelectedItem = match;
        }

        SelectDeviceByGuid(ThrottleDevices, _profile.AppConfig.SelectedThrottleDeviceGuid);
        SelectDeviceByGuid(StickDevices, _profile.AppConfig.SelectedStickDeviceGuid);
    }

    private static void SelectDeviceByGuid(System.Windows.Controls.ComboBox combo, Guid? guid)
    {
        if (guid == null) return;

        var items = combo.ItemsSource as IEnumerable<object>;
        if (items == null) return;

        var match = items.FirstOrDefault(x =>
        {
            var devProp = x.GetType().GetProperty("Device");
            if (devProp?.GetValue(x) is DeviceInstance dev)
                return dev.InstanceGuid == guid.Value;
            return false;
        });

        if (match != null) combo.SelectedItem = match;
    }


    private string DeviceName(Guid guid)
        => _deviceNamesByGuid.TryGetValue(guid, out var n) ? n : guid.ToString();

    private ActiveProfile LoadActiveProfile()
    {
        var appConfig = _store.LoadAppConfig();

        var throttleSettings = _store.LoadProfile<ThrottleSettings>(appConfig.ThrottleProfilePath);
        var gunSettings = _store.LoadProfile<GunHoldSettings>(appConfig.GunProfilePath);
        var bindings = _store.LoadBindings(appConfig.BindingsPath);

        return new ActiveProfile
        {
            AppConfig = appConfig,
            ThrottleSettings = throttleSettings,
            GunSettings = gunSettings,
            Bindings = bindings
        };
    }

    private void Start()
    {
        Stop();

        if (!TryGetSelectedDevices(out var audioDev, out var throttleDev, out var stickDev))
            return;

        _profile = LoadActiveProfile();

        SetupEngine(audioDev);
        StartPollingDevices();
        BuildBindingsFromConfig(throttleDev, stickDev);
        RenderBindings();

        StartEngineTimer();

        LastInputLabel.Text = "None";
    }

    private bool TryGetSelectedDevices(
        out MMDevice audioDevice,
        out DeviceInstance throttleDevice,
        out DeviceInstance stickDevice
    )
    {
        audioDevice = null!;
        throttleDevice = null!;
        stickDevice = null!;

        if (AudioDevices.SelectedItem is not MMDevice audio)
        {
            MessageBox.Show("Select an audio device.");
            return false;
        }

        if (ThrottleDevices.SelectedValue is not DeviceInstance throttle)
        {
            MessageBox.Show("Select a Throttle device.");
            return false;
        }

        if (StickDevices.SelectedValue is not DeviceInstance stick)
        {
            MessageBox.Show("Select a Stick device.");
            return false;
        }

        audioDevice = audio;
        throttleDevice = throttle;
        stickDevice = stick;
        return true;
    }

    private void SetupEngine(MMDevice audioDev)
    {
        _eval = new BindingEvaluator(_profile.ThrottleSettings);

        _engine = new RumbleEngine();
        _engine.Start(audioDev, _profile.ThrottleSettings);
        _engine.Enabled = true;
    }

    private void StartPollingDevices()
    {
        _poller = new MultiJoystickPoller();
        _poller.PollError += ex => Dispatcher.Invoke(() => LastInputLabel.Text = "Poll error: " + ex.Message);
        _poller.StateReceived += OnState;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var joys = _inputSvc.ListJoysticks();

        _deviceNamesByGuid.Clear();

        foreach (var dev in joys)
        {
            _deviceNamesByGuid[dev.InstanceGuid] = dev.InstanceName;

            var js = _inputSvc.Open(dev, hwnd);
            _poller.Add(dev.InstanceGuid, js);
        }

        _poller.Start(5);
    }

    private void BuildBindingsFromConfig(DeviceInstance throttleDevice, DeviceInstance stickDevice)
    {
        lock (_sync)
        {
            _bindings.Clear();
            _prevButtonsByDevice.Clear();
            _latestStateByDevice.Clear();

            if (_profile.Bindings.Count == 0)
            {
                _profile.Bindings = CreateDefaultBindingConfigs(throttleDevice, stickDevice);
                _store.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);
            }

            foreach (var config in _profile.Bindings)
            {
                _bindings.Add(new BindingDefinition
                {
                    DeviceGuid = config.DeviceGuid,
                    DeviceName = DeviceName(config.DeviceGuid),
                    Kind = config.Kind,
                    AxisName = config.AxisName,
                    ButtonIndex = config.ButtonIndex,
                    Effect = config.Effect,
                    Intensity = config.Intensity
                });
            }
        }
    }

    private List<BindingConfig> CreateDefaultBindingConfigs(DeviceInstance throttleDevice, DeviceInstance stickDevice)
    {
        var axisName = _profile.ThrottleSettings.DefaultAxisName;
        var gunButton = _profile.GunSettings.DefaultButtonIndex;

        return new List<BindingConfig>
    {
        new BindingConfig
        {
            DeviceName = "Throttle Axis",
            Kind = BindingKind.Axis,
            DeviceGuid = throttleDevice.InstanceGuid,
            AxisName = axisName,
            Effect = RumbleEffectType.ThrottleAxis,
            Intensity = 1.0f
        },
        new BindingConfig
        {
            DeviceName = "Gun Fire",
            Kind = BindingKind.Button,
            DeviceGuid = stickDevice.InstanceGuid,
            ButtonIndex = gunButton,
            Effect = RumbleEffectType.Gun,
            Intensity = 0.5f
        }
    };
    }


    private void RenderBindings()
    {
        _bindingRows.Clear();
        lock (_sync)
        {
            foreach (var b in _bindings)
                _bindingRows.Add(new BindingRow(b));
        }
    }

    private void StartEngineTimer()
    {
        _engineTimer?.Stop();
        _engineTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
        _engineTimer.Tick += (_, _) => EngineTick();
        _engineTimer.Start();

        _lastTick = Environment.TickCount64;
    }

    private void OnState(Guid deviceGuid, JoystickState state)
    {
        lock (_sync)
        {
            _latestStateByDevice[deviceGuid] = state;
            HandleTransitionsAndLogger(deviceGuid, state);
        }

        Dispatcher.BeginInvoke(() =>
        {
            LastInputLabel.Text = _logger.LastText;
        });

        foreach (var row in _bindingRows.Where(r => r.Def.DeviceGuid == deviceGuid))
        {
            if (row.Def.Kind == BindingKind.Button && row.Def.ButtonIndex is int bi)
            {
                var buttons = state.Buttons ?? Array.Empty<bool>();
                row.ValueText = (bi >= 0 && bi < buttons.Length) ? (buttons[bi] ? "DOWN" : "UP") : "OUT OF RANGE";
            }
            else if (row.Def.Kind == BindingKind.Axis && row.Def.AxisName != null)
            {
                row.ValueText = $"{row.Def.AxisName}: {GetAxisRaw(state, row.Def.AxisName)}";
            }
        }
    }

    private void HandleTransitionsAndLogger(Guid deviceGuid, JoystickState state)
    {
        var buttons = state.Buttons ?? Array.Empty<bool>();

        if (!_prevButtonsByDevice.TryGetValue(deviceGuid, out var prev) || prev.Length != buttons.Length)
        {
            prev = new bool[buttons.Length];
            _prevButtonsByDevice[deviceGuid] = prev;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            bool isDown = buttons[i];
            bool wasDown = prev[i];

            if (isDown && !wasDown)
            {
                _logger.LogButton(deviceGuid, i, true);
                OnButtonPressed(deviceGuid, i);
            }
            else if (!isDown && wasDown)
            {
                _logger.LogButton(deviceGuid, i, false);
                OnButtonReleased(deviceGuid, i);
            }
        }

        Array.Copy(buttons, prev, buttons.Length);
    }

    private void OnButtonPressed(Guid deviceGuid, int buttonIndex)
    {
        List<BindingDefinition> matches;

        lock (_sync)
        {
            matches = _bindings.Where(x =>
                x.Kind == BindingKind.Button &&
                x.DeviceGuid == deviceGuid &&
                x.ButtonIndex == buttonIndex &&
                x.Effect == RumbleEffectType.Gun
            ).ToList();

            foreach (var b in matches)
            {
                var key = (deviceGuid, buttonIndex);
                if (_activeHolds.ContainsKey(key)) continue;

                var fx = new GunHoldEffect(b.Intensity, _profile.GunSettings);
                _activeHolds[key] = fx;
                _mixer.Add(fx);
            }
        }
    }
    private void OnButtonReleased(Guid deviceGuid, int buttonIndex)
    {
        lock (_sync)
        {
            var key = (deviceGuid, buttonIndex);
            if (_activeHolds.TryGetValue(key, out var fx))
            {
                fx.Stop();
                _activeHolds.Remove(key);
            }
        }
    }

    private void EngineTick()
    {
        if (_engine == null || _eval == null) return;

        var now = Environment.TickCount64;
        var dt = (now - _lastTick) / 1000f;
        if (dt <= 0f) dt = 0.005f;
        if (dt > 0.1f) dt = 0.1f;
        _lastTick = now;

        float axisSum = 0f;
        bool enabled = true;
        float effects;

        lock (_sync)
        {
            foreach (var b in _bindings.Where(x => x.Kind == BindingKind.Axis))
            {
                if (b.AxisName == null) continue;
                if (!_latestStateByDevice.TryGetValue(b.DeviceGuid, out var s)) continue;

                var (amp, en) = _eval.Evaluate(b.DeviceGuid, s, b);
                axisSum += amp * b.Intensity;
                enabled = enabled && en;
            }

            effects = _mixer.Update(dt);
        }

        float total = Math.Clamp(axisSum + effects, 0f, 1f);
        _engine.Enabled = enabled;
        _engine.SetTargetAmplitude(total);
    }

    private static int GetAxisRaw(JoystickState s, string axisName) => axisName switch
    {
        "X" => s.X,
        "Y" => s.Y,
        "Z" => s.Z,
        "RotationX" => s.RotationX,
        "RotationY" => s.RotationY,
        "RotationZ" => s.RotationZ,
        "Slider0" => (s.Sliders != null && s.Sliders.Length > 0) ? s.Sliders[0] : 0,
        "Slider1" => (s.Sliders != null && s.Sliders.Length > 1) ? s.Sliders[1] : 0,
        _ => 0
    };
    
    private void Stop()
    {
        StopEngineTimer();
        StopPolling();
        ClearRuntimeState();
        ClearBindingsUI();
        DisposeEngine();

        LastInputLabel.Text = "None";
    }


    private void StopEngineTimer()
    {
        _engineTimer?.Stop();
        _engineTimer = null;
    }

    private void StopPolling()
    {
        if (_poller == null) return;

        _poller.StateReceived -= OnState;
        _poller.Dispose();
        _poller = null;
    }

    private void ClearRuntimeState()
    {
        lock (_sync)
        {
            _mixer.StopAll();
            _activeHolds.Clear();

            _latestStateByDevice.Clear();
            _prevButtonsByDevice.Clear();
            _bindings.Clear();
        }
    }

    private void ClearBindingsUI()
    {
        Dispatcher.Invoke(() => _bindingRows.Clear());
    }

    private void DisposeEngine()
    {
        _engine?.Dispose();
        _engine = null;
        _eval = null;
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveProfile();
        Stop();
        _inputSvc.Dispose();
        base.OnClosed(e);
    }

    private void SaveProfile()
    {
        _store.SaveAppConfig(_profile.AppConfig);
        _store.SaveProfile(_profile.AppConfig.ThrottleProfilePath, _profile.ThrottleSettings);
        _store.SaveProfile(_profile.AppConfig.GunProfilePath, _profile.GunSettings);
        _store.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);
    }
}
