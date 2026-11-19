using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Gameplay;

internal sealed class CollectibleItem(string id, Vector3 position, int requiredPortal = -1)
{
    private const float DissolveDuration = .82f;

    public string Id { get; } = id;
    public Vector3 Position { get; } = position;
    public int RequiredPortal { get; } = requiredPortal;
    public bool Collected { get; private set; }
    public float DissolveProgress => Collected ? Math.Clamp(DissolveElapsed / DissolveDuration, 0f, 1f) : 0f;
    public Vector2 DissolveOriginUv { get; private set; } = new(.5f, .5f);
    private float DissolveElapsed { get; set; }

    public void Collect(Vector3 from)
    {
        Vector3 direction = from - Position;
        if (direction.LengthSquared() > .0001f)
        {
            direction = Vector3.Normalize(direction);
            DissolveOriginUv = new Vector2(
                MathF.Atan2(direction.Z, direction.X) / MathF.Tau + .5f,
                .5f - MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)) / MathF.PI);
        }
        Collected = true;
    }
    public void Update(float elapsed)
    {
        if (Collected) DissolveElapsed += elapsed;
    }
}

internal sealed class CollectibleParticle(Vector3 position, Vector3 velocity, float lifetime, float size)
{
    public Vector3 Position { get; private set; } = position;
    public Vector3 Velocity { get; private set; } = velocity;
    public float Lifetime { get; } = lifetime;
    public float Size { get; } = size;
    public float Age { get; private set; }
    public float Opacity => Math.Clamp(1f - Age / Lifetime, 0f, 1f);

    public void Update(float elapsed)
    {
        Age += elapsed;
        Velocity = new Vector3(Velocity.X * MathF.Max(0f, 1f - elapsed * 1.4f),
            Velocity.Y - 4.2f * elapsed,
            Velocity.Z * MathF.Max(0f, 1f - elapsed * 1.4f));
        Position += Velocity * elapsed;
    }
}

internal sealed class CollectibleManager
{
    private const float PickupRadius = .8f;
    private static readonly Random ParticleRandom = new();
    private readonly ScenePortalPair[] portalPairs;
    private readonly bool[] visitedPairs;
    private readonly List<CollectibleItem> items = new();
    private readonly List<CollectibleParticle> particles = new();

    public IReadOnlyList<CollectibleItem> Items => items;
    public IReadOnlyList<CollectibleParticle> Particles => particles;
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

    public void Update(Vector2 feet, float elevation, float height, float elapsed, bool allowCollection)
    {
        elapsed = Math.Clamp(elapsed, 0f, .1f);
        foreach (CollectibleItem item in items) item.Update(elapsed);
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            particles[i].Update(elapsed);
            if (particles[i].Opacity <= 0f) particles.RemoveAt(i);
        }
        if (!allowCollection || Completed) return;

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
                item.Collect(nearest);
                CollectedCount++;
                SpawnCollectionBurst(item.Position);
            }
        }
    }

    private void SpawnCollectionBurst(Vector3 center)
    {
        for (int i = 0; i < 24; i++)
        {
            float angle = (float)(ParticleRandom.NextDouble() * Math.PI * 2.0);
            float horizontal = .45f + (float)ParticleRandom.NextDouble() * 1.35f;
            Vector3 velocity = new(MathF.Cos(angle) * horizontal,
                .65f + (float)ParticleRandom.NextDouble() * 2.2f,
                MathF.Sin(angle) * horizontal);
            particles.Add(new CollectibleParticle(center, velocity,
                .55f + (float)ParticleRandom.NextDouble() * .5f,
                .035f + (float)ParticleRandom.NextDouble() * .04f));
        }
    }

    public string Objective =>
        Completed ? "Fase concluida!" :
        UsedPortalPairs < TotalPortalPairs
            ? "Atravesse os portais e explore as torres e rampas"
            : "Explore as ilhas e colete os itens que faltam";
}
