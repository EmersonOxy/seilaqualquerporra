using System;
using System.Numerics;

namespace Fps.Desktop.Scenes;

internal sealed class SceneDefinition(
    float size, float tileSize, Vector2 spawn, SceneBox[] boxes,
    ScenePortalPair[] portalPairs, SceneRamp[] ramps, float baseSize = 50f,
    SceneCylinder[]? cylinders = null, SceneLadder[]? ladders = null,
    SceneCollectible[]? collectibles = null, SceneWater[]? water = null,
    SceneGrass[]? grass = null)
{
    private readonly SceneCylinder[] sceneCylinders = cylinders ?? Array.Empty<SceneCylinder>();
    private readonly SceneCollectible[] sceneCollectibles = collectibles ?? Array.Empty<SceneCollectible>();
    private readonly SceneLadder[] sceneLadders = ladders ?? Array.Empty<SceneLadder>();
    private readonly SceneWater[] sceneWater = water ?? Array.Empty<SceneWater>();
    private readonly SceneGrass[] sceneGrass = grass ?? Array.Empty<SceneGrass>();

    public float Size { get; } = size;
    public float BaseSize { get; } = baseSize;
    public float TileSize { get; } = tileSize;
    public Vector2 Spawn { get; } = spawn;
    public ReadOnlySpan<SceneBox> Boxes => boxes;
    public ReadOnlySpan<SceneRamp> Ramps => ramps;
    public ReadOnlySpan<ScenePortalPair> PortalPairs => portalPairs;
    public ReadOnlySpan<SceneCylinder> Cylinders => sceneCylinders;
    public ReadOnlySpan<SceneLadder> Ladders => sceneLadders;
    public ReadOnlySpan<SceneCollectible> Collectibles => sceneCollectibles;
    public ReadOnlySpan<SceneWater> Water => sceneWater;
    public ReadOnlySpan<SceneGrass> Grass => sceneGrass;
    public ScenePortalPair? Portals => portalPairs.Length > 0 ? portalPairs[0] : null;
}
