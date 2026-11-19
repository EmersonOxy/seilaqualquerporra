using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Physics;

// Collision and walkable support are resolved against the SAME object transform
// used by the renderer, including rotated boxes, cylinders, slopes and stair treads.
internal sealed class CollisionWorld
{
    private readonly SceneBox[] boxes;
    private readonly SceneRamp[] ramps;
    private readonly SceneCylinder[] cylinders;
    private readonly SceneLadder[] ladders;
    private readonly ScenePortalPair[] portalPairs;
    private readonly float halfWorld;
    private readonly float halfBase;
    private const float MaxStep = .42f;
    private const float MaxDownSnap = .38f;

    public ReadOnlySpan<ScenePortalPair> PortalPairs => portalPairs;
    public ScenePortalPair? Portals => portalPairs.Length > 0 ? portalPairs[0] : null;
    public CollisionWorld(SceneDefinition scene)
    {
        halfWorld = scene.Size * .5f;
        halfBase = scene.BaseSize * .5f;
        portalPairs = scene.PortalPairs.ToArray();
        boxes = scene.Boxes.ToArray();
        ramps = scene.Ramps.ToArray();
        cylinders = scene.Cylinders.ToArray();
        ladders = scene.Ladders.ToArray();
    }

    private bool IsThroughFloorHole(Vector2 feet, float surfaceY, float radius)
    {
        foreach (ScenePortalPair pair in portalPairs)
        {
            if (IsHole(pair.First, feet, surfaceY, radius) ||
                IsHole(pair.Second, feet, surfaceY, radius)) return true;
        }
        return false;
    }

    private static bool IsHole(ScenePortal p, Vector2 point, float surfaceY, float radius) =>
        p.InFloor && MathF.Abs(surfaceY - p.Center.Y) < .08f && p.Contains(point, radius);

    private static Quaternion RampRotation(SceneRamp ramp) =>
        Quaternion.CreateFromAxisAngle(Vector3.Normalize(ramp.RotationAxis),
            ramp.AngleDegrees * MathF.PI / 180f);

    private static bool CapsuleHitsBox(Vector2 feet, float elevation,
        float height, float radius, Vector3 center, Vector3 size, Quaternion orientation)
    {
        Quaternion inverse = Quaternion.Inverse(orientation);
        Vector3 half = size * .5f;
        // Seven short intervals keep the capsule collision stable against thin objects.
        for (int i = 0; i <= 8; i++)
        {
            float y = elevation + radius + (height - 2f * radius) * i / 8f;
            Vector3 local = Vector3.Transform(new Vector3(feet.X, y, feet.Y) - center, inverse);
            Vector3 closest = Vector3.Clamp(local, -half, half);
            if (Vector3.DistanceSquared(local, closest) < radius * radius - .00002f)
                return true;
        }
        return false;
    }

    private static bool CapsuleHitsCylinder(Vector2 feet, float elevation,
        float height, float radius, SceneCylinder shape)
    {
        Quaternion inverse = Quaternion.Inverse(SceneTransform.Orientation(shape.RotationDegrees));
        float rx = shape.Size.X * .5f, rz = shape.Size.Z * .5f, hy = shape.Size.Y * .5f;
        for (int i = 0; i <= 8; i++)
        {
            float y = elevation + radius + (height - 2f * radius) * i / 8f;
            Vector3 local = Vector3.Transform(new Vector3(feet.X, y, feet.Y) - shape.Center, inverse);
            float normalized = MathF.Sqrt(local.X * local.X / (rx * rx) + local.Z * local.Z / (rz * rz));
            float horizontal = MathF.Max(0f, normalized - 1f) * MathF.Min(rx, rz);
            float vertical = MathF.Max(0f, MathF.Abs(local.Y) - hy);
            if (horizontal * horizontal + vertical * vertical < radius * radius - .00002f)
                return true;
        }
        return false;
    }

    public bool Fits(Vector2 feet, float height, float radius, float elevation = 0f)
    {
        float bound = halfWorld - radius;
        if (MathF.Abs(feet.X) > bound || MathF.Abs(feet.Y) > bound || height < radius * 2f)
            return false;

        foreach (SceneBox box in boxes)
        {
            if (!box.Solid || IsThroughFloorHole(feet, box.Center.Y + box.Size.Y / 2f, radius))
                continue;
            if (CapsuleHitsBox(feet, elevation, height, radius, box.Center, box.Size,
                SceneTransform.Orientation(box.RotationDegrees))) return false;
        }
        foreach (SceneCylinder cylinder in cylinders)
        {
            if (cylinder.Solid && CapsuleHitsCylinder(feet, elevation, height, radius, cylinder))
                return false;
        }
        foreach (SceneRamp ramp in ramps)
        {
            // For a floor slope, the feet are measured vertically while the
            // capsule radius is perpendicular to the surface: touching is valid.
            if (ramp.TryGetHeight(feet, out float walkingY) && elevation >= walkingY - .035f)
                continue;
            if (CapsuleHitsBox(feet, elevation, height, radius, ramp.Center, ramp.BoxSize,
                RampRotation(ramp))) return false;
        }
        Vector3 lower = new(feet.X, elevation + radius, feet.Y);
        Vector3 upper = new(feet.X, elevation + height - radius, feet.Y);
        foreach (ScenePortalPair pair in portalPairs)
        {
            if (BlocksFrame(pair.First, lower, upper, radius) ||
                BlocksFrame(pair.Second, lower, upper, radius)) return false;
        }
        return true;
    }

    private static bool BlocksFrame(ScenePortal portal, Vector3 lower, Vector3 upper, float radius)
    {
        if (portal.InFloor || !OverlapsSlab(portal.SignedDistance(lower),
            portal.SignedDistance(upper), .12f + radius)) return false;
        float halfExtent = portal.Radius + .34f + radius;
        Vector3 a = lower - portal.Center;
        Vector3 b = upper - portal.Center;
        return OverlapsSlab(Vector3.Dot(a, portal.Tangent), Vector3.Dot(b, portal.Tangent), halfExtent)
            && OverlapsSlab(Vector3.Dot(a, portal.Bitangent), Vector3.Dot(b, portal.Bitangent), halfExtent)
            && !portal.FitsOpening(lower, upper, radius);
    }

    private static bool OverlapsSlab(float a, float b, float extent) =>
        MathF.Min(a, b) < extent && MathF.Max(a, b) > -extent;

    private static bool BoxTopAt(Vector2 feet, SceneBox box, out float top)
    {
        Quaternion q = SceneTransform.Orientation(box.RotationDegrees);
        Vector3 normal = Vector3.Transform(Vector3.UnitY, q);
        if (normal.Y < .65f)
        {
            top = 0f;
            return false;
        }
        Vector3 roof = box.Center + normal * box.Size.Y * .5f;
        top = roof.Y - (normal.X * (feet.X - roof.X) + normal.Z * (feet.Y - roof.Z)) / normal.Y;
        Vector3 local = Vector3.Transform(new Vector3(feet.X, top, feet.Y) - box.Center,
            Quaternion.Inverse(q));
        return MathF.Abs(local.X) <= box.Size.X * .5f + .04f &&
               MathF.Abs(local.Z) <= box.Size.Z * .5f + .04f;
    }

    private static bool CylinderTopAt(Vector2 feet, SceneCylinder cylinder, out float top)
    {
        Quaternion q = SceneTransform.Orientation(cylinder.RotationDegrees);
        Vector3 normal = Vector3.Transform(Vector3.UnitY, q);
        if (normal.Y < .7f)
        {
            top = 0f; return false;
        }
        Vector3 roof = cylinder.Center + normal * cylinder.Size.Y * .5f;
        top = roof.Y - (normal.X * (feet.X - roof.X) + normal.Z * (feet.Y - roof.Z)) / normal.Y;
        Vector3 p = Vector3.Transform(new Vector3(feet.X, top, feet.Y) - cylinder.Center,
            Quaternion.Inverse(q));
        float x = p.X / (cylinder.Size.X * .5f);
        float z = p.Z / (cylinder.Size.Z * .5f);
        return x * x + z * z <= 1.02f;
    }

    private bool TrySupport(Vector2 feet, float minY, float maxY, float radius, out float support)
    {
        bool found = false;
        support = float.NegativeInfinity;
        if (MathF.Abs(feet.X) < halfBase && MathF.Abs(feet.Y) < halfBase &&
            !IsThroughFloorHole(feet, 0f, radius) && minY <= 0f && 0f <= maxY)
        {
            support = 0f; found = true;
        }

        foreach (SceneBox box in boxes)
        {
            if (!box.Solid || !BoxTopAt(feet, box, out float y) ||
                y < minY || y > maxY || IsThroughFloorHole(feet, y, radius)) continue;
            if (y > support) { support = y; found = true; }
        }
        foreach (SceneCylinder cylinder in cylinders)
        {
            if (!cylinder.Solid || !CylinderTopAt(feet, cylinder, out float y) ||
                y < minY || y > maxY) continue;
            if (y > support) { support = y; found = true; }
        }
        foreach (SceneRamp ramp in ramps)
        {
            if (ramp.TryGetHeight(feet, out float y) &&
                y >= minY && y <= maxY && y > support)
            {
                support = y; found = true;
            }
        }
        return found;
    }

    public bool GroundBelow(Vector2 feet, float elevation, float radius, out float ground) =>
        TrySupport(feet, elevation - .09f, elevation + .05f, radius, out ground);

    public bool OnRamp(Vector2 feet, float elevation)
    {
        foreach (SceneRamp r in ramps)
            if (r.TryGetHeight(feet, out float h) && MathF.Abs(h - elevation) < .20f)
                return true;
        return false;
    }

    public Vector2 Move(Vector2 feet, Vector2 delta, float height, float radius,
        ref float elevation, bool grounded)
    {
        feet = MoveAxis(feet, new Vector2(delta.X, 0f), height, radius, ref elevation, grounded);
        feet = MoveAxis(feet, new Vector2(0f, delta.Y), height, radius, ref elevation, grounded);
        return feet;
    }

    private Vector2 MoveAxis(Vector2 origin, Vector2 delta, float height, float radius,
        ref float elevation, bool grounded)
    {
        if (delta == Vector2.Zero) return origin;
        float bound = halfWorld - radius;
        Vector2 goal = Vector2.Clamp(origin + delta, new Vector2(-bound), new Vector2(bound));

        if (TryPosition(goal, height, radius, elevation, grounded, out float newY))
        {
            elevation = newY;
            return goal;
        }

        float low = 0f, high = 1f, bestY = elevation;
        for (int i = 0; i < 14; i++)
        {
            float middle = (low + high) * .5f;
            Vector2 point = Vector2.Lerp(origin, goal, middle);
            if (TryPosition(point, height, radius, elevation, grounded, out float y))
            { low = middle; bestY = y; }
            else high = middle;
        }
        elevation = bestY;
        return Vector2.Lerp(origin, goal, low);
    }

    private bool TryPosition(Vector2 point, float height, float radius,
        float originY, bool grounded, out float nextY)
    {
        nextY = originY;
        if (grounded && TrySupport(point, originY - MaxDownSnap,
            originY + MaxStep, radius, out float support))
            nextY = support;
        // A step is accepted only if the entire capsule fits at the elevated position.
        if (Fits(point, height, radius, nextY)) return true;
        // Prevent sticky edges: if no support was detected, test current height.
        if (nextY != originY && Fits(point, height, radius, originY))
        {
            nextY = originY;
            return true;
        }
        return false;
    }

    public float MoveVertical(Vector2 feet, float elevation, float target, float height, float radius)
    {
        if (target < elevation &&
            TrySupport(feet, target, elevation + .002f, radius, out float support))
            return support;

        if (Fits(feet, height, radius, target)) return target;

        float good = elevation, bad = target;
        for (int i = 0; i < 14; i++)
        {
            float mid = (good + bad) * .5f;
            if (Fits(feet, height, radius, mid)) good = mid;
            else bad = mid;
        }
        return good;
    }

    public bool TryGetLadder(Vector2 feet, float elevation, float height, out SceneLadder ladder)
    {
        foreach (SceneLadder l in ladders)
        {
            Quaternion q = SceneTransform.Orientation(l.RotationDegrees);
            Vector3 local = Vector3.Transform(new Vector3(feet.X, elevation + height * .45f, feet.Y) -
                l.Center, Quaternion.Inverse(q));
            if (MathF.Abs(local.X) <= l.Size.X * .5f + .28f &&
                MathF.Abs(local.Z) <= l.Size.Z * .5f + .48f &&
                elevation + height > l.Center.Y - l.Size.Y * .5f &&
                elevation < l.Center.Y + l.Size.Y * .5f + .15f)
            {
                ladder = l; return true;
            }
        }
        ladder = default;
        return false;
    }
}
