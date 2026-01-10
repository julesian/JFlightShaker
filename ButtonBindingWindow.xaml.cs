using JFlightShaker.Config;
using JFlightShaker.Input;
using System.Globalization;
using System.Windows;

namespace JFlightShaker.UI;

public partial class ButtonBindingWindow : Window
{
    private readonly BindingConfig _binding;
    private readonly Guid _deviceGuid;
    private IDisposable? _subscription;

    private bool _isListening;

    public ButtonBindingWindow(
        BindingConfig binding,
        Guid deviceGuid)
    {
        InitializeComponent();

        _binding = binding;
        _deviceGuid = deviceGuid;

        Title = "Edit Button";

        ListenBtn.Click += (_, _) => ToggleListen();
        CancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        SaveBtn.Click += (_, _) => OnSave();

        Loaded += (_, _) => _subscription = InputPollingService.Shared.ListenForButtons(this, OnButtonPressedEdge);
        Closed += (_, _) =>
        {
            _subscription?.Dispose();
            _subscription = null;
        };

        LoadState();
        UpdateListenUI();
    }

    private void LoadState()
    {
        ButtonTextBox.Text = _binding.ButtonIndex is int bi
            ? bi.ToString(CultureInfo.InvariantCulture)
            : "";
    }

    private void ToggleListen()
    {
        _isListening = !_isListening;

        if (_isListening)
            InputPollingService.Shared.ArmListening(_deviceGuid);

        UpdateListenUI();
    }

    private void UpdateListenUI()
    {
        ListenBtn.Content = _isListening ? "Listening..." : "Listen";
        ListenBtn.IsEnabled = true;
    }

    private void OnButtonPressedEdge(Guid guid, int buttonIndex)
    {
        if (!_isListening) return;
        if (guid != _deviceGuid) return;

        Dispatcher.Invoke(() =>
        {
            ButtonTextBox.Text = buttonIndex.ToString(CultureInfo.InvariantCulture);
            _isListening = false;
            UpdateListenUI();
        });
    }

    private void OnSave()
    {
        if (!int.TryParse(ButtonTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bi) || bi < 0)
        {
            System.Windows.MessageBox.Show("Enter a valid button index (0+).");
            return;
        }

        _binding.ButtonIndex = bi;

        // enforce one scheme
        _binding.AxisName = null;
        _binding.AxisMin = null;
        _binding.AxisMax = null;
        _binding.InvertAxis = false;

        DialogResult = true;
        Close();
    }
}
