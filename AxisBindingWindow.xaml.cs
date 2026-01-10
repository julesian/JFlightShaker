using JFlightShaker.Config;
using SharpDX.DirectInput;
using System.Globalization;
using System.Windows;

namespace JFlightShaker.UI;

public partial class AxisBindingWindow : Window
{
    private readonly IReadOnlyList<DeviceOption> _devices;
    private readonly Func<Guid, Joystick?> _openJoystick;
    private readonly BindingConfig _binding;

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

        var axes = BindingUiHelper.GetDeviceAxes(_openJoystick, devGuid);
        AxisCombo.ItemsSource = axes;

        if (!string.IsNullOrWhiteSpace(_binding.AxisName))
            AxisCombo.SelectedItem = _binding.AxisName;

        if (AxisCombo.SelectedItem == null && axes.Count > 0)
            AxisCombo.SelectedIndex = 0;

        MinBox.Text = (_binding.AxisMin ?? 0f).ToString("0.###", CultureInfo.InvariantCulture);
        MaxBox.Text = (_binding.AxisMax ?? 1f).ToString("0.###", CultureInfo.InvariantCulture);
        InvertCheck.IsChecked = _binding.InvertAxis;
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
}
