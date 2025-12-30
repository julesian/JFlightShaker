using System.Collections.Generic;
using NAudio.CoreAudioApi;

namespace JFlightShaker.Service;

public sealed class AudioDeviceService
{
    public List<MMDevice> GetRenderDevices()
    {
        using var en = new MMDeviceEnumerator();
        return new List<MMDevice>(en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active));
    }
}
