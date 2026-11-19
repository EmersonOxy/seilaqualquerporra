using System;
using System.Numerics;

namespace Fps.Desktop.Scenes;

internal static class SceneTransform
{
    public static Quaternion Orientation(Vector3 degrees) =>
        Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(
            degrees.Y * MathF.PI / 180f, degrees.X * MathF.PI / 180f,
            degrees.Z * MathF.PI / 180f));

    public static Vector3 LocalPoint(Vector3 world, Vector3 center, Vector3 degrees) =>
        Vector3.Transform(world - center, Quaternion.Inverse(Orientation(degrees)));

    public static void ToAxisAngle(Quaternion quaternion, out Vector3 axis, out float angle)
    {
        quaternion = Quaternion.Normalize(quaternion);
        float w = Math.Clamp(quaternion.W, -1f, 1f);
        angle = 2f * MathF.Acos(w) * 180f / MathF.PI;
        float s = MathF.Sqrt(MathF.Max(0f, 1f - w * w));
        axis = s > .0001f ? Vector3.Normalize(new Vector3(quaternion.X, quaternion.Y, quaternion.Z)) : Vector3.UnitY;
    }

    public static Quaternion RampOrientation(float yawDegrees, float rise, float run) =>
        Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDegrees * MathF.PI / 180f)
            * Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.Atan2(rise, run)));
}
