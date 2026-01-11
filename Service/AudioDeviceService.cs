using NAudio.CoreAudioApi;

namespace JFlightShaker.Service;

public sealed class AudioDeviceService
{
    public List<AudioDeviceOption> GetRenderDevices()
    {
        using var en = new MMDeviceEnumerator();
        return en.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(device => new AudioDeviceOption(device.ID, device.FriendlyName))
            .ToList();
    }

    public MMDevice? GetRenderDeviceById(string id)
    {
        using var en = new MMDeviceEnumerator();
        try
        {
            return en.GetDevice(id);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
