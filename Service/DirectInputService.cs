using System;
using System.Collections.Generic;
using SharpDX.DirectInput;

namespace JFlightShaker.Service;

public sealed class DirectInputService : IDisposable
{
    private readonly DirectInput _directInput = new();

    public IReadOnlyList<DeviceInstance> ListJoysticks()
    {
        return _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly)
                           .ToList();
    }


    public Joystick Open(DeviceInstance dev, IntPtr hwnd)
    {
        var js = new Joystick(_directInput, dev.InstanceGuid);
        js.Properties.BufferSize = 128;
        js.SetCooperativeLevel(hwnd, CooperativeLevel.Background | CooperativeLevel.NonExclusive);
        js.Acquire();
        return js;
    }

    public void Dispose() => _directInput.Dispose();
}
