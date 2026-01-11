using JFlightShaker.Config;
using JFlightShaker.Enum;
using JFlightShaker.Service;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace JFlightShaker.UI;

public partial class ButtonBindingWindow : Window
{
    private sealed class TriggerOption
    {
        public TriggerType Trigger { get; init; }
        public string Label { get; init; } = "";

        public override string ToString() => Label;
    }

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

        TriggerCombo.ItemsSource = new List<TriggerOption>
        {
            new() { Trigger = TriggerType.Hold, Label = "Hold" },
            new() { Trigger = TriggerType.Press, Label = "Trigger" }
        };

        LoadState();
        UpdateListenUI();
    }

    private void LoadState()
    {
        ButtonTextBox.Text = _binding.ButtonIndex is int bi
            ? bi.ToString(CultureInfo.InvariantCulture)
            : "";

        var allowed = EffectBindingRules.GetAllowedTriggers(_binding.Effect);
        if (!allowed.Contains(_binding.Trigger))
            _binding.Trigger = allowed.FirstOrDefault();

        TriggerCombo.SelectedItem = TriggerCombo.ItemsSource is IEnumerable<TriggerOption> options
            ? options.FirstOrDefault(o => o.Trigger == _binding.Trigger) ?? options.FirstOrDefault()
            : null;

        if (allowed.Count <= 1)
        {
            TriggerCombo.IsEnabled = false;
            TriggerLabel.Opacity = 0.6;
        }
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

        if (TriggerCombo.SelectedItem is TriggerOption option)
        {
            _binding.Trigger = option.Trigger;
        }

        // enforce one scheme
        _binding.AxisName = null;
        _binding.AxisMin = null;
        _binding.AxisMax = null;
        _binding.InvertAxis = false;

        DialogResult = true;
        Close();
    }
}
