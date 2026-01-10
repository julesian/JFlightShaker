using JFlightShaker.Audio;
using JFlightShaker.Config;
using JFlightShaker.Core;
using JFlightShaker.Enum;
using JFlightShaker.Input;
using JFlightShaker.Service;
using JFlightShaker.UI;
using NAudio.CoreAudioApi;
using SharpDX.DirectInput;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace JFlightShaker;

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

    public event Action<Guid, int>? ButtonPressedEdge;

    // UI
    private EffectRow? SelectedEffectRow => BindingsGrid.SelectedItem as EffectRow;
    private readonly ObservableCollection<EffectRow> _effectRows = new();
    private bool _isRunning;

    public bool IsRunning => _isRunning;

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

    private readonly List<BindingDefinition> _bindings = new();

    private bool _isRestoringSelections;

    public MainWindow()
    {
        InitializeComponent();
        InitializeEffects();

        try
        {
            _store = new ConfigStoreService();
            _logger = new GlobalInputLogger(DeviceName);

            BindingsGrid.ItemsSource = _effectRows;

            InitializeActionButtons();

            StartStopBtn.Click += (_, _) => ToggleStartStop();
            UpdateStartStopUI();

            AudioDevices.SelectionChanged += AudioDevices_SelectionChanged;

            RefreshDevices();
            LoadActiveProfileAndApply();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Startup error");
            throw;
        }
    }

    private void AudioDevices_SelectionChanged(
        object sender, 
        System.Windows.Controls.SelectionChangedEventArgs e
    )
    {
        if (_isRestoringSelections) return;
        SaveSelectedAudioDevice();
    }

    private void BindingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedEffectRow == null) return;

        if (BindingsGrid.SelectedItem == null) return;

        EditSelectedEffect();
    }

    private void InitializeEffects()
    {
        _effectRows.Clear();

        _effectRows.Add(new EffectRow(
            RumbleEffectType.ThrottleAxis,
            "Throttle"
        ));

        _effectRows.Add(new EffectRow(
            RumbleEffectType.Gun,
            "Gun Fire"
        ));
    }

    private void InitializeActionButtons()
    {
        BindingsGrid.SelectionChanged += (_, _) => UpdateEffectActionButtons();

        EditBtn.Click += (_, _) => EditSelectedEffect();
        UnbindBtn.Click += (_, _) => UnbindSelectedEffect();

        UpdateEffectActionButtons();
    }

    private void UpdateEffectActionButtons()
    {
        var hasSelection = SelectedEffectRow != null;
        EditBtn.IsEnabled = hasSelection;
        UnbindBtn.IsEnabled = hasSelection && SelectedEffectRow!.IsBound;
    }



    private void RefreshDevices()
    {
        var audio = _audioSvc.GetRenderDevices();
        AudioDevices.ItemsSource = audio;
        AudioDevices.DisplayMemberPath = "FriendlyName";

        var joys = _inputSvc.ListJoysticks();

        _deviceNamesByGuid.Clear();
        foreach (var d in joys)
            _deviceNamesByGuid[d.InstanceGuid] = d.InstanceName;

        var items = joys
            .Select(d => new
            {
                Device = d,
                Label = $"{d.InstanceName}  |  {d.InstanceGuid}"
            })
            .ToList();
    }

    private void LoadActiveProfileAndApply()
    {
        _profile = LoadActiveProfile();
        ApplyBindingsToEffects();
        ApplySavedSelections();
    }

    private void ApplySavedSelections()
    {
        _isRestoringSelections = true;
        try
        {
            var audio = (IEnumerable<MMDevice>)AudioDevices.ItemsSource;

            if (_profile.AppConfig.SelectedAudioDeviceId is string aid)
            {
                var match = audio.FirstOrDefault(x => x.ID == aid);
                if (match != null)
                {
                    AudioDevices.SelectedItem = match;
                    return;
                }
            }

            // Fallback
            var first = audio.FirstOrDefault();
            if (first != null)
                AudioDevices.SelectedItem = first;
        }
        finally
        {
            _isRestoringSelections = false;
        }
    }


    private void ApplyBindingsToEffects()
    {
        foreach (var row in _effectRows)
        {
            var allowedKinds = GetAllowedKinds(row.Effect);
            var bindings = _profile.Bindings
                .Where(b => b.Effect == row.Effect)
                .Where(b => b.DeviceGuid != null)
                .Where(b => allowedKinds.Contains(b.Kind))
                .ToList();

            if (bindings.Count == 0)
            {
                row.SetUnbound();
                continue;
            }

            var kinds = bindings
                .Select(b => b.Kind)
                .Distinct()
                .OrderBy(k => k.ToString())
                .ToList();

            if (kinds.Count == 1)
            {
                var binding = bindings.First();
                string bindingText = binding.Kind switch
                {
                    BindingKind.Axis =>
                        $"{DeviceName(binding.DeviceGuid!.Value)} / {binding.AxisName}",

                    BindingKind.Button =>
                        $"{DeviceName(binding.DeviceGuid!.Value)} / Button {binding.ButtonIndex}",

                    _ => "Unknown"
                };

                row.SetBound(bindingText, binding.Intensity);
            }
            else
            {
                var kindLabel = string.Join(" + ", kinds);
                row.SetBound($"Multiple ({kindLabel})", 0f);
            }
        }
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
    private void UpdateStartStopUI()
    {
        StartStopBtn.Content = _isRunning ? "Stop" : "Start";
    }

    private void ToggleStartStop()
    {
        if (_isRunning)
            Stop();
        else
            Start();
    }

    private void Start()
    {
        Stop();

        if (!TryGetSelectedDevices(out var audioDev, out var throttleDev, out var stickDev))
            return;

        _profile = LoadActiveProfile();

        if (_profile.Bindings.Count == 0)
        {
            _profile.Bindings = CreateDefaultBindingConfigs(throttleDev, stickDev);
            _store?.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);

            ApplyBindingsToEffects();
            UpdateEffectActionButtons();
        }

        SetupEngine(audioDev);
        StartPollingDevices();
        BuildBindingsFromProfile();
        SetEffectsRunning(true);
        StartEngineTimer();

        LastInputLabel.Text = "None";

        _isRunning = true;
        UpdateStartStopUI();
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

        audioDevice = audio;
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

    private void BuildBindingsFromProfile()
    {
        lock (_sync)
        {
            _bindings.Clear();
            _prevButtonsByDevice.Clear();
            _latestStateByDevice.Clear();

            foreach (var config in _profile.Bindings)
            {
                if (config.DeviceGuid == null) continue;
                if (!IsKindAllowed(config.Effect, config.Kind)) continue;

                _bindings.Add(new BindingDefinition
                {
                    DeviceGuid = config.DeviceGuid.Value,
                    DeviceName = DeviceName(config.DeviceGuid.Value),
                    Kind = config.Kind,
                    AxisName = config.AxisName,
                    AxisMin = config.AxisMin,
                    AxisMax = config.AxisMax,
                    InvertAxis = config.InvertAxis,
                    ButtonIndex = config.ButtonIndex,
                    Effect = config.Effect,
                    Intensity = config.Intensity
                });
            }
        }
    }

    private List<BindingConfig> CreateDefaultBindingConfigs(
        DeviceInstance throttleDevice,
        DeviceInstance stickDevice
    )
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


    private void SetEffectsRunning(bool isRunning)
    {
        foreach (var row in _effectRows)
            row.SetRunning(isRunning);
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
                ButtonPressedEdge?.Invoke(deviceGuid, i);
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

    private void OnButtonPressed(
        Guid deviceGuid, 
        int buttonIndex
    )
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
        SetEffectsRunning(false);
        StopEngineTimer();
        StopPolling();
        ClearRuntimeState();
        DisposeEngine();

        LastInputLabel.Text = "None";

        _isRunning = false;
        UpdateStartStopUI();
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

    private void SaveSelectedAudioDevice()
    {
        if (_store == null) return;

        if (AudioDevices.SelectedItem is MMDevice audio)
        {
            _profile.AppConfig.SelectedAudioDeviceId = audio.ID;
            _store.SaveAppConfig(_profile.AppConfig);
        }
    }

    private void UnbindSelectedEffect()
    {
        var row = SelectedEffectRow;
        if (row == null) return;

        var bindings = _profile.Bindings.Where(b => b.Effect == row.Effect).ToList();
        if (bindings.Count == 0) return;

        foreach (var binding in bindings)
        {
            // Clear Binding
            binding.DeviceGuid = null;
            binding.AxisName = null;
            binding.ButtonIndex = null;
        }

        _store?.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);

        BuildBindingsFromProfile();
        ApplyBindingsToEffects();
        UpdateEffectActionButtons();
    }

    private void EditSelectedEffect()
    {
        var row = SelectedEffectRow;
        if (row == null) return;

        var allowedKinds = GetAllowedKinds(row.Effect);
        var defaultKind = allowedKinds.First();

        var binding = _profile.Bindings.FirstOrDefault(b => b.Effect == row.Effect);
        if (binding == null)
        {
            binding = new BindingConfig { Effect = row.Effect, Kind = BindingKind.Axis, Intensity = 1f };
            binding = new BindingConfig { Effect = row.Effect, Kind = defaultKind, Intensity = 1f };
            _profile.Bindings.Add(binding);
        }
        else if (!allowedKinds.Contains(binding.Kind))
        {
            binding.Kind = defaultKind;
            binding.AxisName = null;
            binding.AxisMin = null;
            binding.AxisMax = null;
            binding.ButtonIndex = null;
        }

        var devices = _deviceNamesByGuid
            .Select(kv => new DeviceOption { Guid = kv.Key, Name = kv.Value })
            .OrderBy(x => x.Name)
            .ToList();

        var win = new EditBindingWindow(
            devices,
            TryOpenJoystick,
            binding,
            allowedKinds,
            row.EffectName
        )
        {
            Owner = this
        };

        var ok = win.ShowDialog() == true;
        if (!ok) return;

        _store?.SaveBindings(_profile.AppConfig.BindingsPath, _profile.Bindings);

        BuildBindingsFromProfile();
        ApplyBindingsToEffects();
        UpdateEffectActionButtons();
    }


    private Joystick? TryOpenJoystick(Guid guid)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var di = _inputSvc.ListJoysticks().FirstOrDefault(x => x.InstanceGuid == guid);
            if (di.InstanceGuid == Guid.Empty) return null;

            return _inputSvc.Open(di, hwnd);
        }
        catch
        {
            return null;
        }
    }
    private static IReadOnlyList<BindingKind> GetAllowedKinds(RumbleEffectType effect)
    {
        return effect switch
        {
            RumbleEffectType.ThrottleAxis => new[] { BindingKind.Axis },
            RumbleEffectType.Gun => new[] { BindingKind.Button },
            _ => new[] { BindingKind.Axis }
        };
    }

    private static bool IsKindAllowed(RumbleEffectType effect, BindingKind kind)
        => GetAllowedKinds(effect).Contains(kind);
    
}
