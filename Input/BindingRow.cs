using System.ComponentModel;
using System.Runtime.CompilerServices;
using ThrottleShaker.Input;
using ThrottleShaker.Enum;

public sealed class BindingRow : INotifyPropertyChanged
{
    public BindingDefinition Def { get; }

    public BindingRow(BindingDefinition def) => Def = def;

    public string Device => $"{Def.DeviceName}";
    public string Key => Def.Kind == BindingKind.Axis ? (Def.AxisName ?? "-") : $"Button {Def.ButtonIndex}";
    public string Effect => Def.Effect.ToString();
    public string Intensity => Def.Intensity.ToString("0.00");

    private string _valueText = "-";
    public string ValueText { get => _valueText; set { _valueText = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
