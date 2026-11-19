using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Gameplay;

internal sealed class CollectibleItem(string id, Vector3 position, int requiredPortal = -1)
{
    public string Id { get; } = id;
    public Vector3 Position { get; } = position;
    public int RequiredPortal { get; } = requiredPortal;
    public bool Collected { get; private set; }
    public void Collect() => Collected = true;
}

internal sealed class CollectibleManager
{
    private const float PickupRadius = .8f;
    private readonly ScenePortalPair[] portalPairs;
    private readonly bool[] visitedPairs;
    private readonly List<CollectibleItem> items = new();

    public IReadOnlyList<CollectibleItem> Items => items;
    public int TotalCount => items.Count;
    public int CollectedCount { get; private set; }
    public int TotalPortalPairs => portalPairs.Length;
    public int UsedPortalPairs { get; private set; }
    public bool Completed => CollectedCount == TotalCount && TotalCount > 0 &&
        UsedPortalPairs == TotalPortalPairs;

    public CollectibleManager(SceneDefinition scene)
    {
        portalPairs = scene.PortalPairs.ToArray();
        visitedPairs = new bool[portalPairs.Length];
        foreach (SceneCollectible collectible in scene.Collectibles)
            items.Add(new CollectibleItem(collectible.Name, collectible.Position, collectible.RequiredPortal));
    }

    public void NotifyPortalTraversed(ScenePortal entry, ScenePortal exit)
    {
        for (int i = 0; i < portalPairs.Length; i++)
        {
            ScenePortalPair pair = portalPairs[i];
            if ((entry == pair.First && exit == pair.Second) ||
                (entry == pair.Second && exit == pair.First))
            {
                if (!visitedPairs[i])
                {
                    visitedPairs[i] = true;
                    UsedPortalPairs++;
                }
                break;
            }
        }
    }

    public bool Available(CollectibleItem item) =>
        item.RequiredPortal < 0 ||
        (item.RequiredPortal < visitedPairs.Length && visitedPairs[item.RequiredPortal]);

    public void Update(Vector2 feet, float elevation, float height)
    {
        if (Completed)
            return;

        float lower = elevation + .22f;
        float upper = elevation + height - .22f;
        foreach (CollectibleItem item in items)
        {
            if (item.Collected || !Available(item))
                continue;

            float nearestY = Math.Clamp(item.Position.Y, lower, upper);
            Vector3 nearest = new(feet.X, nearestY, feet.Y);
            if (Vector3.DistanceSquared(nearest, item.Position) <= PickupRadius * PickupRadius)
            {
                item.Collect();
                CollectedCount++;
            }
        }
    }

    public string Objective =>
        Completed ? "Fase concluida!" :
        UsedPortalPairs < TotalPortalPairs
            ? "Atravesse os portais e explore as torres e rampas"
            : "Explore as ilhas e colete os itens que faltam";
}
