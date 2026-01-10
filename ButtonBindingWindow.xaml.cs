using JFlightShaker.Config;
using System.Globalization;
using System.Windows;

namespace JFlightShaker.UI;

public partial class ButtonBindingWindow : Window
{
    private readonly BindingConfig _binding;
    private readonly Guid _deviceGuid;
    private readonly Func<bool> _isRunning;
    private readonly Action<Action<Guid, int>> _subscribe;
    private readonly Action<Action<Guid, int>> _unsubscribe;

    private bool _isListening;

    public ButtonBindingWindow(
        BindingConfig binding,
        Guid deviceGuid,
        Func<bool> isRunning,
        Action<Action<Guid, int>> subscribe,
        Action<Action<Guid, int>> unsubscribe)
    {
        InitializeComponent();

        _binding = binding;
        _deviceGuid = deviceGuid;
        _isRunning = isRunning;
        _subscribe = subscribe;
        _unsubscribe = unsubscribe;

        Title = "Edit Button";

        ListenBtn.Click += (_, _) => ToggleListen();
        CancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        SaveBtn.Click += (_, _) => OnSave();

        Loaded += (_, _) => _subscribe(OnButtonPressedEdge);
        Closed += (_, _) => _unsubscribe(OnButtonPressedEdge);

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
        // if not running, don't enter listening mode
        if (!_isRunning())
        {
            System.Windows.MessageBox.Show("Start the app first to listen for button input.");
            _isListening = false;
            UpdateListenUI();
            return;
        }

        _isListening = !_isListening;
        UpdateListenUI();
    }

    private void UpdateListenUI()
    {
        if (!_isRunning()) _isListening = false;

        ListenBtn.Content = _isListening ? "Listening..." : "Listen";
        ListenBtn.IsEnabled = true; // keep clickable; we show message if stopped
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
