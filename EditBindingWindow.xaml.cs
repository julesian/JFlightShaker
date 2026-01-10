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
        KindCombo.SelectionChanged += (_, _) => OnKindChanged();
        EditControlBtn.Click += (_, _) => OnEditControl();

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
    }

    private void OnKindChanged()
    {
        if (KindCombo.SelectedItem is not BindingKind kind) return;

        if (kind == BindingKind.Axis)
        {
            _binding.ButtonIndex = null;
        }
        else
        {
            _binding.AxisName = null;
            _binding.AxisMin = null;
            _binding.AxisMax = null;
        }

        // TODO: add binding info in the bottom of the UI so user can see current binding details
    }

    private void OnEditControl()
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

        var main = Owner as MainWindow;
        if (main == null)
        {
            MessageBox.Show("Main window not found.");
            return;
        }

        Window? editor = kind switch
        {
            BindingKind.Axis => new AxisBindingWindow(_devices, _openJoystick, _binding) { Owner = this },

            BindingKind.Button => new ButtonBindingWindow(
                _binding,
                dev.Guid,
                () => main.IsRunning,
                handler => main.ButtonPressedEdge += handler,
                handler => main.ButtonPressedEdge -= handler
            )
            { Owner = this },

            _ => null
        };

        if (editor == null) return;

        var ok = editor.ShowDialog() == true;
        if (!ok) return;

        if (kind == BindingKind.Axis)
        {
            _binding.ButtonIndex = null;
        }
        else
        {
            _binding.AxisName = null;
            _binding.AxisMin = null;
            _binding.AxisMax = null;
            _binding.InvertAxis = false;
        }
    }

    private void OnDeviceChanged()
    {
        // TODO: for binding display info
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
            _binding.ButtonIndex = null;

            if (string.IsNullOrWhiteSpace(_binding.AxisName))
            {
                MessageBox.Show("Select an axis with Edit Control.");
                return;
            }
        }
        else
        {
            _binding.AxisMin = null;
            _binding.AxisMax = null;
            _binding.InvertAxis = false;
            if (_binding.ButtonIndex is null)
            {
                MessageBox.Show("Select a button with Edit Control.");
                return;
            }
        }

        DialogResult = true;
        Close();
    }
}
