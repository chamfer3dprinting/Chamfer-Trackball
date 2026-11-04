namespace Chamfer.Trackball.Core;

// Ball displacement sets a bounded velocity while an explicit navigation hold
// remains active. Release/reset never emits a stored velocity or catch-up step.
public sealed class HeldRateMotion
{
    double x, y, wheel, vx, vy, vw, previous = double.NaN;
    static double Rate(double displacement, double range, double maximum)
    {
        double amount = Math.Clamp(displacement / range, -1, 1);
        double magnitude = Math.Max(0, (Math.Abs(amount) - .035) / .965);
        return Math.Sign(amount) * Math.Pow(magnitude, 1.5) * maximum;
    }
    static float Integrate(ref double velocity, double desired, double dt)
    {
        const double tau = .025;
        double decay = Math.Exp(-dt / tau);
        double travel = desired * dt + (velocity - desired) * tau * (1 - decay);
        velocity = desired + (velocity - desired) * decay;
        return (float)travel;
    }
    public MotionFrame Step(float dx, float dy, float scroll, double milliseconds, NavMode mode, bool finish = false)
    {
        if (!float.IsFinite(dx) || !float.IsFinite(dy) || !float.IsFinite(scroll) || !double.IsFinite(milliseconds))
            throw new ArgumentException("Invalid cruise motion.");
        if (finish) { Reset(); return default; }
        double dt = double.IsNaN(previous) ? 1.0 / 60 : (milliseconds - previous) / 1000;
        if (dt <= 0 || dt > .1) { Reset(); dt = 1.0 / 60; }
        previous = milliseconds;
        x = Math.Clamp(x + dx, -120, 120);
        y = Math.Clamp(y + dy, -120, 120);
        wheel = Math.Clamp(wheel + scroll, -360, 360);
        double maximum = mode == NavMode.Zoom ? 240 : 1600;
        return new(Integrate(ref vx, Rate(x, 120, maximum), dt),
            Integrate(ref vy, Rate(y, 120, maximum), dt),
            Integrate(ref vw, Rate(wheel, 360, 600), dt), 0);
    }
    public void Reset() { x = y = wheel = vx = vy = vw = 0; previous = double.NaN; }
}
