using JFlightShaker.Config;
using JFlightShaker.Enum;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace JFlightShaker.UI;

public sealed class DeviceOption
{
    public Guid Guid { get; init; }
    public string Name { get; init; } = "";

    public override string ToString() => Name;
}

public partial class EditBindingWindow : Window
{
    private readonly IReadOnlyList<DeviceOption> _devices;
    private readonly Func<Guid, Joystick?> _openJoystick;
    private readonly BindingConfig _binding;

    public EditBindingWindow(
        IReadOnlyList<DeviceOption> devices,
        Func<Guid, Joystick?> openJoystick,
        BindingConfig binding,
        string effectName
    )
    {
        InitializeComponent();

        Title = $"Edit Binding - {effectName}";

        _devices = devices;
        _openJoystick = openJoystick;
        _binding = binding;

        DeviceCombo.ItemsSource = _devices;
        KindCombo.ItemsSource = new[] { BindingKind.Axis, BindingKind.Button };

        DeviceCombo.SelectionChanged += (_, _) => OnDeviceChanged();
        KindCombo.SelectionChanged += (_, _) => UpdateKindUI();

        IntensitySlider.ValueChanged += (_, _) =>
        {
            IntensityValueLabel.Text = IntensitySlider.Value.ToString("0.00", CultureInfo.InvariantCulture);
        };

        CancelBtn.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        SaveBtn.Click += (_, _) => OnSave();

        LoadInitialState();
    }

    private void LoadInitialState()
    {
        // Device
        if (_binding.DeviceGuid is Guid g)
        {
            DeviceCombo.SelectedItem = _devices.FirstOrDefault(x => x.Guid == g);
        }

        if (DeviceCombo.SelectedItem == null && _devices.Count > 0)
            DeviceCombo.SelectedIndex = 0;

        // Kind
        KindCombo.SelectedItem = _binding.Kind;

        // Intensity
        IntensitySlider.Value = Math.Clamp(_binding.Intensity, 0f, 1f);
        IntensityValueLabel.Text = IntensitySlider.Value.ToString("0.00", CultureInfo.InvariantCulture);

        // Load axes for current device
        OnDeviceChanged();

        // Preselect axis/button
        if (!string.IsNullOrWhiteSpace(_binding.AxisName))
            AxisCombo.SelectedItem = _binding.AxisName;

        if (_binding.ButtonIndex is int bi)
            ButtonTextBox.Text = bi.ToString(CultureInfo.InvariantCulture);

        UpdateKindUI();
    }

    private void OnDeviceChanged()
    {
        AxisCombo.ItemsSource = null;

        if (DeviceCombo.SelectedItem is not DeviceOption dev)
            return;

        var axes = GetDeviceAxes(dev.Guid);
        AxisCombo.ItemsSource = axes;

        // If device changed, clear inputs
        if (_binding.DeviceGuid != dev.Guid)
        {
            AxisCombo.SelectedItem = null;
            ButtonTextBox.Text = "";
        }

        if (KindCombo.SelectedItem is BindingKind kind && kind == BindingKind.Axis)
        {
            if (AxisCombo.SelectedItem == null && axes.Count > 0)
                AxisCombo.SelectedIndex = 0;
        }
    }

    private void UpdateKindUI()
    {
        var isAxis = (KindCombo.SelectedItem is BindingKind k) && k == BindingKind.Axis;

        AxisCombo.IsEnabled = isAxis;
        ButtonTextBox.IsEnabled = !isAxis;

        AxisCombo.Opacity = isAxis ? 1.0 : 0.45;
        ButtonTextBox.Opacity = isAxis ? 0.45 : 1.0;
    }

    private List<string> GetDeviceAxes(Guid deviceGuid)
    {
        var js = _openJoystick(deviceGuid);
        if (js == null) return new List<string>();

        try
        {
            var objs = js.GetObjects();

            // NOTE: On many SharpDX versions, Rotation/Slider flags aren't present.
            // Axis flag is enough;
            var axes = objs
                .Where(o => (o.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0)
                .Select(o => o.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList();

            return axes
                .Select(MapAxisName)
                .Distinct()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
        finally
        {
            js.Dispose();
        }
    }

    private static string MapAxisName(string directInputName)
    {
        var n = directInputName.Trim();

        if (n.Contains("X Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationX";
        if (n.Contains("Y Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationY";
        if (n.Contains("Z Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationZ";

        if (n.Contains("X Axis", StringComparison.OrdinalIgnoreCase)) return "X";
        if (n.Contains("Y Axis", StringComparison.OrdinalIgnoreCase)) return "Y";
        if (n.Contains("Z Axis", StringComparison.OrdinalIgnoreCase)) return "Z";

        if (n.Contains("Slider", StringComparison.OrdinalIgnoreCase)) return "Slider0";

        return n;
    }

    private void OnSave()
    {
        if (DeviceCombo.SelectedItem is not DeviceOption dev)
        {
            MessageBox.Show("Select a device.");
            return;
        }

        if (KindCombo.SelectedItem is not BindingKind kind)
        {
            MessageBox.Show("Select a type.");
            return;
        }

        _binding.DeviceGuid = dev.Guid;
        _binding.DeviceName = dev.Name;
        _binding.Kind = kind;
        _binding.Intensity = (float)IntensitySlider.Value;

        if (kind == BindingKind.Axis)
        {
            if (AxisCombo.SelectedItem is not string axis || string.IsNullOrWhiteSpace(axis))
            {
                MessageBox.Show("Select an axis.");
                return;
            }

            _binding.AxisName = axis;
            _binding.ButtonIndex = null;
        }
        else
        {
            if (!int.TryParse(ButtonTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi) || bi < 0)
            {
                MessageBox.Show("Enter a valid button index (0+).");
                return;
            }

            _binding.ButtonIndex = bi;
            _binding.AxisName = null;
        }

        DialogResult = true;
        Close();
    }
}
