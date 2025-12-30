using SharpDX.DirectInput;
using ThrottleShaker.Config;
using ThrottleShaker.Input;
using ThrottleShaker.Enum;

namespace ThrottleShaker.Core;

public sealed class BindingEvaluator
{
    private readonly ThrottleSettings _settings;

    // smoothing per (device + axis)
    private readonly Dictionary<(Guid dev, string axis), float> _smoothed = new();

    public BindingEvaluator(ThrottleSettings settings) => _settings = settings;

    public (float amp, bool enabled) Evaluate(Guid deviceGuid, JoystickState state, BindingDefinition binding)
    {
        if (binding.Kind != BindingKind.Axis || string.IsNullOrWhiteSpace(binding.AxisName))
            return (0f, true);

        int raw = GetAxisRaw(state, binding.AxisName);
        float norm = Math.Clamp(raw / 65535f, 0f, 1f);

        if (_settings.InvertAxis)
            norm = 1f - norm;

        float deadzone = Math.Clamp(_settings.Deadzone, 0f, 0.95f);
        if (norm <= deadzone) norm = 0f;
        else norm = (norm - deadzone) / (1f - deadzone);

        float curve = _settings.ResponseCurve <= 0f ? 1f : _settings.ResponseCurve;
        norm = (float)Math.Pow(norm, curve);

        float baseAmp = Math.Clamp(_settings.BaselineAmp, 0f, 1f);
        float topAmp = Math.Clamp(_settings.TopAmp, 0f, 1f);
        if (topAmp < baseAmp) topAmp = baseAmp;

        float target = baseAmp + norm * (topAmp - baseAmp);

        float smoothing = Math.Clamp(_settings.AmpSmoothing, 0f, 0.999f);
        var key = (deviceGuid, binding.AxisName);

        _smoothed.TryGetValue(key, out var prev);
        float smoothed = (prev * smoothing) + (target * (1f - smoothing));
        _smoothed[key] = smoothed;

        return (Math.Clamp(smoothed, 0f, 1f), true);
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
}
