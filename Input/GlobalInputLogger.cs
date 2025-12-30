using System;
using SharpDX.DirectInput;

namespace ThrottleShaker.Input;

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
        LastText = $"Last: {(_nameOf(deviceGuid))} Button {buttonIndex} {(isDown ? "DOWN" : "UP")}";
        LastAtMs = Environment.TickCount64;
    }

    public void LogAxis(Guid deviceGuid, string axisName, int rawValue)
    {
        LastText = $"Last: {(_nameOf(deviceGuid))} {axisName} = {rawValue}";
        LastAtMs = Environment.TickCount64;
    }
}
