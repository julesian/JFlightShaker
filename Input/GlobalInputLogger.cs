using System;
using System.Globalization;

namespace JFlightShaker.Input;

public sealed class GlobalInputLogger
{
    public string LastText { get; private set; } = "None";
    public long LastAtMs { get; private set; }

    private readonly Func<Guid, string> _nameOf;

    public GlobalInputLogger(Func<Guid, string> nameOf)
    {
        _nameOf = nameOf;
    }

    public void LogButton(Guid deviceGuid, int buttonIndex, bool isDown)
    {
        var dev = _nameOf(deviceGuid);
        var state = isDown ? "Down" : "Up";
        LastText = $"{dev} | Button {buttonIndex} | {state}";
        LastAtMs = Environment.TickCount64;
    }

    public void LogAxis(Guid deviceGuid, string axisName, int rawValue)
    {
        var dev = _nameOf(deviceGuid);

        var clamped = Math.Clamp(rawValue, 0, 65535);
        var normalized = clamped / 65535.0;

        LastText = $"{dev} | {axisName} | {normalized.ToString("0.00", CultureInfo.InvariantCulture)}";
        LastAtMs = Environment.TickCount64;
    }
}
