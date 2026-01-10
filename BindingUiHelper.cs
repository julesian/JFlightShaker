using SharpDX.DirectInput;

namespace JFlightShaker;

public static class BindingUiHelper
{
    public static List<string> GetDeviceAxes(Func<Guid, Joystick?> openJoystick, Guid deviceGuid)
    {
        var js = openJoystick(deviceGuid);
        if (js == null) return new List<string>();

        try
        {
            var objs = js.GetObjects();

            var axes = objs
                .Where(o => (o.ObjectId.Flags & DeviceObjectTypeFlags.Axis) != 0)
                .Select(o => o.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct()
                .ToList();

            return axes
                .Select(MapAxisName)
                .Distinct()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
        finally
        {
            js.Dispose();
        }
    }

    public static string MapAxisName(string directInputName)
    {
        var n = directInputName.Trim();

        if (n.Contains("X Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationX";
        if (n.Contains("Y Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationY";
        if (n.Contains("Z Rotation", StringComparison.OrdinalIgnoreCase)) return "RotationZ";

        if (n.Contains("X Axis", StringComparison.OrdinalIgnoreCase)) return "X";
        if (n.Contains("Y Axis", StringComparison.OrdinalIgnoreCase)) return "Y";
        if (n.Contains("Z Axis", StringComparison.OrdinalIgnoreCase)) return "Z";

        if (n.Contains("Slider", StringComparison.OrdinalIgnoreCase)) return "Slider0";

        return n;
    }
}
