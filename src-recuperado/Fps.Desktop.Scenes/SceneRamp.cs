using System;
using System.Numerics;

namespace Fps.Desktop.Scenes;

// A smooth walkable sloped surface. Start and End are at the TOP of the ramp.
internal readonly record struct SceneRamp(Vector3 Start, Vector3 End, float Width, float Thickness, Surface Surface)
{
    public Vector2 Direction
    {
        get
        {
            Vector2 delta = new(End.X - Start.X, End.Z - Start.Z);
            return Vector2.Normalize(delta);
        }
    }

    public float Run => Vector2.Distance(new Vector2(Start.X, Start.Z), new Vector2(End.X, End.Z));
    public float Rise => End.Y - Start.Y;

    public bool TryGetHeight(Vector2 point, out float height)
    {
        Vector2 origin = new(Start.X, Start.Z);
        Vector2 displacement = point - origin;
        Vector2 forward = Direction;
        float along = Vector2.Dot(displacement, forward);
        float sideways = MathF.Abs(forward.X * displacement.Y - forward.Y * displacement.X);
        if (along < -0.01f || along > Run + 0.01f || sideways > Width * 0.5f)
        {
            height = 0f;
            return false;
        }
        height = float.Lerp(Start.Y, End.Y, Math.Clamp(along / Run, 0f, 1f));
        return true;
    }

    public Vector3 RotationAxis
    {
        get
        {
            Vector2 direction = Direction;
            return Vector3.Cross(new Vector3(direction.X, 0f, direction.Y), Vector3.UnitY);
        }
    }

    public float AngleDegrees => MathF.Atan2(Rise, Run) * (180f / MathF.PI);
    public float SlopedLength => MathF.Sqrt(Run * Run + Rise * Rise);

    public Vector3 Center
    {
        get
        {
            Vector2 forward = Direction;
            Vector3 slopeNormal = Vector3.Normalize(new Vector3(-forward.X * Rise / Run, 1f, -forward.Y * Rise / Run));
            return (Start + End) / 2f - slopeNormal * (Thickness * 0.5f);
        }
    }

    public Vector3 BoxSize
    {
        get
        {
            Vector2 forward = Direction;
            return MathF.Abs(forward.X) > MathF.Abs(forward.Y)
                ? new Vector3(SlopedLength, Thickness, Width)
                : new Vector3(Width, Thickness, SlopedLength);
        }
    }
}
