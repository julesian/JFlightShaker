using JFlightShaker.Config;
using JFlightShaker.Helpers;
using JFlightShaker.Input;
using SharpDX.DirectInput;
using System.Globalization;
using System.Windows;

namespace JFlightShaker.UI;

public partial class AxisBindingWindow : Window
{
    private MultiJoystickPoller? _poller;
    private JoystickState? _latestState;
    private readonly IReadOnlyList<DeviceOption> _devices;
    private readonly Func<Guid, Joystick?> _openJoystick;
    private readonly BindingConfig _binding;
    private Guid _deviceGuid;

    public AxisBindingWindow(
        IReadOnlyList<DeviceOption> devices,
        Func<Guid, Joystick?> openJoystick,
        BindingConfig binding)
    {
        InitializeComponent();
        _devices = devices;
        _openJoystick = openJoystick;
        _binding = binding;

        CancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        SaveBtn.Click += (_, _) => OnSave();
        AxisCombo.SelectionChanged += (_, _) => UpdateAxisInputLabel();

        LoadState();
    }

    private void LoadState()
    {
        if (_binding.DeviceGuid is not Guid devGuid)
        {
            MessageBox.Show("No device selected.");
            DialogResult = false;
            Close();
            return;
        }

        _deviceGuid = devGuid;

        var axes = BindingUiHelper.GetDeviceAxes(_openJoystick, devGuid);
        AxisCombo.ItemsSource = axes;

        if (!string.IsNullOrWhiteSpace(_binding.AxisName))
            AxisCombo.SelectedItem = _binding.AxisName;

        if (AxisCombo.SelectedItem == null && axes.Count > 0)
            AxisCombo.SelectedIndex = 0;

        MinBox.Text = (_binding.AxisMin ?? 0f).ToString("0.###", CultureInfo.InvariantCulture);
        MaxBox.Text = (_binding.AxisMax ?? 1f).ToString("0.###", CultureInfo.InvariantCulture);
        InvertCheck.IsChecked = _binding.InvertAxis;

        StartPolling();
    }

    private void OnSave()
    {
        if (AxisCombo.SelectedItem is not string axis || string.IsNullOrWhiteSpace(axis))
        {
            MessageBox.Show("Select an axis.");
            return;
        }

        if (!float.TryParse(MinBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var min))
        {
            MessageBox.Show("Min must be a number.");
            return;
        }

        if (!float.TryParse(MaxBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
        {
            MessageBox.Show("Max must be a number.");
            return;
        }

        min = Math.Clamp(min, 0f, 1f);
        max = Math.Clamp(max, 0f, 1f);

        if (min > max)
        {
            MessageBox.Show("Min must not be greater than Max.");
            return;
        }

        _binding.AxisName = axis;
        _binding.AxisMin = min;
        _binding.AxisMax = max;
        _binding.InvertAxis = InvertCheck.IsChecked == true;

        // enforce one scheme
        _binding.ButtonIndex = null;

        DialogResult = true;
        Close();
    }
    private void StartPolling()
    {
        var js = _openJoystick(_deviceGuid);
        if (js == null)
        {
            AxisInputLabel.Text = "Unable to read device.";
            return;
        }

        _poller = new MultiJoystickPoller();
        _poller.StateReceived += OnState;
        _poller.PollError += ex => Dispatcher.Invoke(() => AxisInputLabel.Text = "Poll error: " + ex.Message);
        _poller.Add(_deviceGuid, js);
        _poller.Start(10);
    }

    private void OnState(Guid deviceGuid, JoystickState state)
    {
        _latestState = state;
        Dispatcher.BeginInvoke(UpdateAxisInputLabel);
    }

    private void UpdateAxisInputLabel()
    {
        if (AxisCombo.SelectedItem is not string axisName || string.IsNullOrWhiteSpace(axisName))
            return;

        var state = _latestState;
        if (state is null) return;

        int raw = GetAxisRaw(state, axisName);
        int clamped = Math.Clamp(raw, 0, 65535);
        double normalized = clamped / 65535.0;

        AxisInputLabel.Text = $"{axisName} | Raw {clamped} | {normalized.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    private static int GetAxisRaw(JoystickState state, string axisName) => axisName switch
    {
        "X" => state.X,
        "Y" => state.Y,
        "Z" => state.Z,
        "RotationX" => state.RotationX,
        "RotationY" => state.RotationY,
        "RotationZ" => state.RotationZ,
        "Slider0" => (state.Sliders != null && state.Sliders.Length > 0) ? state.Sliders[0] : 0,
        "Slider1" => (state.Sliders != null && state.Sliders.Length > 1) ? state.Sliders[1] : 0,
        _ => 0
    };

    protected override void OnClosed(EventArgs e)
    {
        if (_poller != null)
        {
            _poller.StateReceived -= OnState;
            _poller.Dispose();
            _poller = null;
        }

        base.OnClosed(e);
    }
}
