using JFlightShaker.Enum;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JFlightShaker.UI;

public sealed class EffectRow : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public RumbleEffectType Effect { get; }
    public string EffectName { get; }

    private string _bindingText = "Not Set";
    public string BindingText
    {
        get => _bindingText;
        private set => Set(ref _bindingText, value);
    }

    private string _statusText = "Stopped";
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    private float _intensity;
    public float Intensity
    {
        get => _intensity;
        private set => Set(ref _intensity, value);
    }

    private bool _isBound;
    public bool IsBound
    {
        get => _isBound;
        private set => Set(ref _isBound, value);
    }

    public EffectRow(RumbleEffectType effect, string effectName)
    {
        Effect = effect;
        EffectName = effectName;

        SetUnbound();
    }

    public void SetBound(string bindingText, float intensity)
    {
        BindingText = bindingText;
        Intensity = intensity;
        IsBound = true;
    }

    public void SetUnbound()
    {
        BindingText = "Not Set";
        Intensity = 0f;
        StatusText = "Stopped";
        IsBound = false;
    }

    public void SetRunning(bool running)
    {
        StatusText = running ? "Running" : "Stopped";
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
