using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Gameplay;

internal enum CollectibleRequirement
{
    None,
    FloorToWallPortal,
    WallToFloorPortal
}

internal sealed class CollectibleItem(string id, Vector3 position, CollectibleRequirement requirement)
{
    public string Id { get; } = id;
    public Vector3 Position { get; } = position;
    public CollectibleRequirement Requirement { get; } = requirement;
    public bool Collected { get; private set; }
    public void Collect() => Collected = true;
}

// Independent of rendering and input: the existing PlayerController remains responsible
// for portal travel, crouching, collision and running.
internal sealed class CollectibleManager
{
    private const float PickupRadius = 0.73f;
    private readonly ScenePortalPair? portals;
    private bool reachedWallFromFloor;
    private bool returnedFromWall;
    private readonly List<CollectibleItem> items = new();

    public IReadOnlyList<CollectibleItem> Items => items;
    public int TotalCount => items.Count;
    public int CollectedCount { get; private set; }
    public bool Completed => CollectedCount == TotalCount && TotalCount > 0;
    public bool UsedFloorToWallPortal => reachedWallFromFloor;
    public bool UsedWallToFloorPortal => returnedFromWall;

    public CollectibleManager(SceneDefinition scene)
    {
        portals = scene.Portals;
        Vector2 spawn = scene.Spawn;
        items.Add(new CollectibleItem("spawn", new Vector3(spawn.X - 2f, 1.05f, spawn.Y - 1f), CollectibleRequirement.None));
        // The canopy has 1.24 m of clearance: the character must crouch to reach this item.
        items.Add(new CollectibleItem("canopy", new Vector3(0f, 0.52f, -1.5f), CollectibleRequirement.None));
        items.Add(new CollectibleItem("arena", new Vector3(-8f, 1f, -7f), CollectibleRequirement.None));

        if (portals is { } pair)
        {
            ScenePortal floor = pair.First.InFloor ? pair.First : pair.Second;
            ScenePortal wall = pair.First.InFloor ? pair.Second : pair.First;
            // The wall-exit item appears only after the player actually uses the floor portal.
            items.Add(new CollectibleItem("wall-exit", wall.Center + wall.Normal * 2.15f + new Vector3(0f, -0.8f, 0f), CollectibleRequirement.FloorToWallPortal));
            // The last item requires a return trip through the paired portals.
            items.Add(new CollectibleItem("floor-return", floor.Center + new Vector3(-2.65f, 1.25f, 1.25f), CollectibleRequirement.WallToFloorPortal));
        }
    }

    public void NotifyPortalTraversed(ScenePortal entry, ScenePortal exit)
    {
        if (portals is not { } pair)
            return;

        if (entry == pair.First && exit == pair.Second)
        {
            if (pair.First.InFloor)
                reachedWallFromFloor = true;
            else
                returnedFromWall = true;
        }
        else if (entry == pair.Second && exit == pair.First)
        {
            if (pair.Second.InFloor)
                reachedWallFromFloor = true;
            else
                returnedFromWall = true;
        }
    }

    public bool Available(CollectibleItem item) => item.Requirement switch
    {
        CollectibleRequirement.None => true,
        CollectibleRequirement.FloorToWallPortal => reachedWallFromFloor,
        CollectibleRequirement.WallToFloorPortal => returnedFromWall,
        _ => false
    };

    public void Update(Vector2 feet, float elevation, float height)
    {
        if (Completed)
            return;

        // Measure distance to the upright player capsule, not just eye position.
        float bottom = elevation + 0.22f;
        float top = elevation + height - 0.22f;
        foreach (CollectibleItem item in items)
        {
            if (item.Collected || !Available(item))
                continue;
            float nearestY = Math.Clamp(item.Position.Y, bottom, top);
            Vector3 nearest = new Vector3(feet.X, nearestY, feet.Y);
            if (Vector3.DistanceSquared(nearest, item.Position) <= PickupRadius * PickupRadius)
            {
                item.Collect();
                CollectedCount++;
            }
        }
    }

    public string Objective
    {
        get
        {
            if (Completed)
                return "Todos os itens coletados!";
            if (!reachedWallFromFloor && portals.HasValue)
                return "Atravesse o portal no chao para liberar mais itens";
            if (!returnedFromWall && portals.HasValue)
                return "Volte pelo portal da parede para liberar o ultimo item";
            return "Explore o mapa e colete os itens restantes";
        }
    }
}
