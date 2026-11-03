namespace Chamfer.Trackball.Core;

public sealed class SlimBladeTwist
{
    private float remaining;

    public static bool Applies(string device, NavMode mode, int message) =>
        message == 0x20a && (mode is NavMode.Orbit or NavMode.PanZoom) &&
        device.Contains("HID#VID_047D&PID_80D6&", StringComparison.OrdinalIgnoreCase);

    public void Add(int scroll, NavMode mode)
    {
        // Keep the first response immediate and distribute the rest across
        // the next few camera frames. This still starts from discrete notches.
        float gain = mode == NavMode.Orbit ? .5f : .75f;
        remaining = Math.Clamp(remaining + scroll * gain, -1200f, 1200f);
    }

    public float Step()
    {
        float delta = Math.Abs(remaining) < 1f ? remaining : remaining * .6f;
        remaining -= delta;
        return delta;
    }

    public void Reset() => remaining = 0;
}
