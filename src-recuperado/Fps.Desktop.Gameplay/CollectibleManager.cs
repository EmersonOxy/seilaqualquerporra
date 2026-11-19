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
        Vector2 spawn = scene.Spawn;

        // Collectibles are laid out as a tour of ramps, suspended islands and towers.
        items.Add(new CollectibleItem("inicio", new Vector3(spawn.X - 2f, 1.05f, spawn.Y - 1f)));
        items.Add(new CollectibleItem("agachamento", new Vector3(0f, .52f, -1.5f)));
        items.Add(new CollectibleItem("rampa-leste", new Vector3(30f, 2.05f, -5f)));
        items.Add(new CollectibleItem("ilha-leste", new Vector3(31f, 1f, -10f)));
        items.Add(new CollectibleItem("torre-leste", new Vector3(39f, 3.65f, -7f), 0));
        items.Add(new CollectibleItem("ilha-oeste", new Vector3(-35f, 1f, -22f)));
        items.Add(new CollectibleItem("torre-oeste", new Vector3(-44f, 6.35f, -16f), 1));
        items.Add(new CollectibleItem("pedras-norte", new Vector3(-10f, 1.55f, -30.2f)));
        items.Add(new CollectibleItem("ilha-norte", new Vector3(0f, 1f, -35f)));
        items.Add(new CollectibleItem("torre-norte", new Vector3(0f, 4.75f, -46f)));
        items.Add(new CollectibleItem("ilha-sul", new Vector3(10f, 1f, 35f)));
        items.Add(new CollectibleItem("rampa-sul", new Vector3(4f, 2.55f, 33f)));
        items.Add(new CollectibleItem("torre-final", new Vector3(19f, 6.9f, 42f), 2));
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
