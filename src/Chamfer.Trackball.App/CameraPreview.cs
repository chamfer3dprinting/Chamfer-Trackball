using System.Drawing;
using System.Numerics;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;

namespace Chamfer.Trackball.App;

internal sealed class CameraPreview
{
    static readonly CameraPose InitialView = new(
        new(2.8f, 2.4f, 4.1f), Vector3.Zero, Vector3.UnitY, Vector3.Zero,
        1.14f, 6, 400, 300, false, Vector3.UnitY, Vector3.Zero);

    public NativeServer.Client Client { get; } = new() { App = "demo", IsPreview = true };
    CameraPose saved = InitialView;
    CameraPose? lastReported;

    public CameraPose View => Client.Pose ?? saved;

    public void Remember() => saved = View;

    public void Reset()
    {
        saved = InitialView;
        lastReported = null;
    }

    public void Refresh() => lastReported = null;

    public CameraPose SceneAt(Rectangle bounds, Point cursor)
    {
        var pose = View with { Width = Math.Max(bounds.Width, 1), Height = Math.Max(bounds.Height, 1) };
        var back = Vector3.Normalize(pose.Eye - pose.Target);
        var right = Vector3.Normalize(Vector3.Cross(pose.Up, back));
        var up = Vector3.Cross(back, right);
        float depth = Vector3.Distance(pose.Eye, pose.Target);
        float pixel = pose.Orthographic ? pose.OrthoHeight / pose.Height :
            2 * depth * MathF.Tan(pose.VerticalFov / 2) / pose.Height;
        // The training cube uses the cursor ray at the camera's focus depth.
        var pivot = pose.Target + right * ((cursor.X - bounds.Left - pose.Width / 2f) * pixel)
            - up * ((cursor.Y - bounds.Top - pose.Height / 2f) * pixel);
        return pose with { Pivot = pivot, ZoomPivot = pivot };
    }

    public void SnapFace(Rectangle bounds, Point cursor)
    {
        var pose = SceneAt(bounds, cursor);
        saved = CameraMath.AlignFace(pose, CameraMath.NearestFace(Vector3.Normalize(pose.Eye - pose.Target)));
    }

    public JsonObject? ReadChangedView()
    {
        var pose = View;
        if (lastReported == pose) return null;
        lastReported = pose;
        static JsonArray Vector(Vector3 v) => new(v.X, v.Y, v.Z);
        return new()
        {
            ["eye"] = Vector(pose.Eye), ["target"] = Vector(pose.Target), ["up"] = Vector(pose.Up),
            ["verticalFov"] = pose.VerticalFov, ["orthoHeight"] = pose.OrthoHeight,
            ["orthographic"] = pose.Orthographic
        };
    }
}
