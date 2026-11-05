namespace Chamfer.Trackball.Core;

// A native Studio client can smooth absolute poses on its own display clock.
// Keep legacy/emulated devices on their existing timer path.
public static class NativeDelivery
{
    public static bool Direct(string device,string app,int protocol,NavMode mode,bool steady,bool cruise) =>
        SlimBladeTwist.IsDevice(device) && app=="chamfer3d" && protocol>=2 && steady && !cruise &&
        mode is NavMode.Orbit or NavMode.PanZoom or NavMode.Zoom or NavMode.Navigate;

    public static bool Circle(string device,NavMode mode,bool enabled,bool held) =>
        !(held && SlimBladeTwist.IsDevice(device)) && MotionPolicy.Circle(mode,enabled,held);

    // The Studio bridge bounds its short presentation response at 30 ms.
    public static float DisplayResponse(float milliseconds) =>
        float.IsFinite(milliseconds)?Math.Clamp(milliseconds,6,30):12;
}
