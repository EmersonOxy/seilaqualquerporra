using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Physics;

// Capsule-vs-box collision, height-aware ramps and isolated floating decks.
internal sealed class CollisionWorld
{
    private readonly record struct Box(Vector3 Min, Vector3 Max);
    private readonly Box[] solids;
    private readonly SceneRamp[] ramps;
    private readonly ScenePortalPair[] portalPairs;
    private readonly float halfWorld;
    private readonly float halfBase;

    public ReadOnlySpan<ScenePortalPair> PortalPairs => portalPairs;
    public ScenePortalPair? Portals => portalPairs.Length > 0 ? portalPairs[0] : null;

    public CollisionWorld(SceneDefinition scene)
    {
        halfWorld = scene.Size * .5f;
        halfBase = scene.BaseSize * .5f;
        portalPairs = scene.PortalPairs.ToArray();
        ramps = scene.Ramps.ToArray();
        List<Box> list = new();
        foreach (SceneBox sceneBox in scene.Boxes)
        {
            if (sceneBox.Solid)
                list.Add(new Box(sceneBox.Center - sceneBox.Size * .5f, sceneBox.Center + sceneBox.Size * .5f));
        }
        solids = list.ToArray();
    }

    private ScenePortal? FloorHoleAt(Vector2 point, float margin)
    {
        foreach (ScenePortalPair pair in portalPairs)
        {
            ScenePortal? hole = pair.FloorOpeningAt(point, margin);
            if (hole.HasValue)
                return hole;
        }
        return null;
    }

    private bool IsThroughFloorHole(Vector2 point, float topY, float radius)
    {
        foreach (ScenePortalPair pair in portalPairs)
        {
            if (MatchesHole(pair.First, point, topY, radius) ||
                MatchesHole(pair.Second, point, topY, radius))
                return true;
        }
        return false;
    }

    private static bool MatchesHole(ScenePortal portal, Vector2 point, float topY, float radius) =>
        portal.InFloor && MathF.Abs(topY - portal.Center.Y) < .08f && portal.Contains(point, radius);

    // Returns the highest valid walkable surface below maxY and above minY.
    private bool TrySupport(Vector2 feet, float minY, float maxY, float radius, out float support)
    {
        bool found = false;
        support = float.NegativeInfinity;
        if (MathF.Abs(feet.X) < halfBase && MathF.Abs(feet.Y) < halfBase &&
            !IsThroughFloorHole(feet, 0f, radius) && minY <= 0f && 0f <= maxY)
        {
            support = 0f;
            found = true;
        }

        foreach (Box box in solids)
        {
            if (feet.X < box.Min.X || feet.X > box.Max.X ||
                feet.Y < box.Min.Z || feet.Y > box.Max.Z)
                continue;
            float top = box.Max.Y;
            if (top < minY || top > maxY || IsThroughFloorHole(feet, top, radius))
                continue;
            if (top > support)
            {
                support = top;
                found = true;
            }
        }

        foreach (SceneRamp ramp in ramps)
        {
            if (ramp.TryGetHeight(feet, out float top) && top >= minY && top <= maxY && top > support)
            {
                support = top;
                found = true;
            }
        }
        return found;
    }

    public bool Fits(Vector2 feet, float height, float radius, float elevation = 0f)
    {
        float bound = halfWorld - radius;
        if (MathF.Abs(feet.X) > bound || MathF.Abs(feet.Y) > bound || height < radius * 2f)
            return false;

        foreach (Box box in solids)
        {
            if (IsThroughFloorHole(feet, box.Max.Y, radius))
                continue;
            float dx = MathF.Max(0f, MathF.Max(box.Min.X - feet.X, feet.X - box.Max.X));
            float dy = MathF.Max(0f, MathF.Max(box.Min.Y - (elevation + height - radius),
                                                 elevation + radius - box.Max.Y));
            float dz = MathF.Max(0f, MathF.Max(box.Min.Z - feet.Y, feet.Y - box.Max.Z));
            if (dx * dx + dy * dy + dz * dz < radius * radius - .0000001f)
                return false;
        }

        Vector3 lower = new(feet.X, elevation + radius, feet.Y);
        Vector3 upper = new(feet.X, elevation + height - radius, feet.Y);
        foreach (ScenePortalPair pair in portalPairs)
        {
            if (BlocksFrame(pair.First, lower, upper, radius) ||
                BlocksFrame(pair.Second, lower, upper, radius))
                return false;
        }
        return true;
    }

    private static bool BlocksFrame(ScenePortal portal, Vector3 lower, Vector3 upper, float radius)
    {
        if (portal.InFloor || !OverlapsSlab(portal.SignedDistance(lower),
            portal.SignedDistance(upper), .12f + radius))
            return false;

        float halfExtent = portal.Radius + .34f + radius;
        Vector3 a = lower - portal.Center;
        Vector3 b = upper - portal.Center;
        if (OverlapsSlab(Vector3.Dot(a, portal.Tangent), Vector3.Dot(b, portal.Tangent), halfExtent) &&
            OverlapsSlab(Vector3.Dot(a, portal.Bitangent), Vector3.Dot(b, portal.Bitangent), halfExtent))
            return !portal.FitsOpening(lower, upper, radius);
        return false;
    }

    private static bool OverlapsSlab(float a, float b, float halfExtent)
        => MathF.Min(a, b) < halfExtent && MathF.Max(a, b) > -halfExtent;

    // Move horizontally and step up smooth ramps / small deck edges.
    // The caller's elevation changes only when the player can actually fit.
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
        if (delta == Vector2.Zero)
            return origin;

        float bound = halfWorld - radius;
        Vector2 goal = Vector2.Clamp(origin + delta, new Vector2(-bound), new Vector2(bound));
        if (TryPosition(goal, height, radius, elevation, grounded, out float newY))
        {
            elevation = newY;
            return goal;
        }

        float low = 0f, high = 1f, bestY = elevation;
        for (int i = 0; i < 12; i++)
        {
            float middle = (low + high) * .5f;
            Vector2 point = Vector2.Lerp(origin, goal, middle);
            if (TryPosition(point, height, radius, elevation, grounded, out float stepY))
            {
                low = middle;
                bestY = stepY;
            }
            else high = middle;
        }
        elevation = bestY;
        return Vector2.Lerp(origin, goal, low);
    }

    private bool TryPosition(Vector2 goal, float height, float radius,
        float currentY, bool grounded, out float newY)
    {
        newY = currentY;
        if (grounded && TrySupport(goal, currentY - .24f, currentY + .22f, radius, out float stepY))
            newY = stepY;

        return Fits(goal, height, radius, newY);
    }

    public float MoveVertical(Vector2 feet, float previousY, float targetY, float height, float radius)
    {
        if (targetY < previousY && TrySupport(feet, targetY, previousY + .002f, radius, out float groundY))
            return groundY;

        if (Fits(feet, height, radius, targetY))
            return targetY;

        float valid = previousY, blocked = targetY;
        for (int i = 0; i < 12; i++)
        {
            float mid = (valid + blocked) * .5f;
            if (Fits(feet, height, radius, mid))
                valid = mid;
            else
                blocked = mid;
        }
        return valid;
    }
}
