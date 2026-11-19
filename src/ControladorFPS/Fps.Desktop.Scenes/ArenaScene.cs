using System.Collections.Generic;
using System.Numerics;

namespace Fps.Desktop.Scenes;

internal static class ArenaScene
{
    public static SceneDefinition Create()
    {
        List<SceneBox> boxes = new();
        List<SceneRamp> ramps = new();

        // The checkerboard remains a 50 x 50 starting island. The rest of the
        // world is empty space: missing a jump makes the player fall.
        // Low overhead canopy keeps crouching useful.
        boxes.Add(new SceneBox(new Vector3(0f, 1.36f, -1.5f), new Vector3(4.8f, 0.24f, 3.4f), Surface.Deck));
        foreach (int sideX in new[] { -1, 1 })
        foreach (int sideZ in new[] { -1, 1 })
            boxes.Add(new SceneBox(new Vector3(sideX * 2.27f, .62f, -1.5f + sideZ * 1.57f), new Vector3(.26f, 1.24f, .26f), Surface.Steel));

        // Floating islands around the original 50 m area (top surfaces at Y=0).
        Deck(boxes, 33f, 0f, -4f, 18f, 24f);
        Deck(boxes, -38f, 0f, -16f, 22f, 22f);
        Deck(boxes, 0f, 0f, -42f, 25f, 22f);
        Deck(boxes, 10f, 0f, 38f, 20f, 20f);

        // Stepping stones across voids. Sprinting and jumping are needed.
        Deck(boxes, -14f, .3f, -27.1f, 4.4f, 3.2f);
        Deck(boxes, -10f, .65f, -30.2f, 4f, 3f);
        Deck(boxes, -5.6f, .25f, -31.1f, 4f, 4f);
        Deck(boxes, -25.6f, .35f, -12.5f, 3.4f, 4f);
        Deck(boxes, 9f, .45f, 26.9f, 4.5f, 3.4f);
        Deck(boxes, 16f, .8f, 27.5f, 4.4f, 4f);

        // Towers of varying heights, each with a level roof to land on.
        Tower(boxes, 39f, 2.8f, -5f, 9f, 9f);      // east / easy
        Tower(boxes, -44f, 5.5f, -16f, 10f, 9f);   // west / tall
        Tower(boxes, 0f, 4f, -46f, 8f, 8f);         // north / medium
        Tower(boxes, 4f, 3f, 42f, 10f, 9f);         // southern intermediate
        Tower(boxes, 19f, 6f, 42f, 9f, 9f);         // southern high tower
        Tower(boxes, 19f, 1.5f, -20f, 5f, 5f);     // small jumping tower
        Tower(boxes, -18f, 1.1f, -30.5f, 5f, 5f);  // small jumping tower

        // Smooth ramp surfaces actually participate in collision/support physics.
        Ramp(ramps, 26f, 0f, -5f, 34.5f, 2.8f, -5f, 3.6f);
        Ramp(ramps, 39f, 2.8f, -.5f, 39f, 0f, 6.5f, 3.2f); // descending east
        Ramp(ramps, -27f, 0f, -16f, -39f, 5.5f, -16f, 3.5f);
        Ramp(ramps, -44f, 5.5f, -20.5f, -44f, 0f, -26.5f, 2.6f); // descending west
        Ramp(ramps, 0f, 0f, -32f, 0f, 4f, -42f, 3.4f);
        Ramp(ramps, 4f, 4f, -46f, 11f, 0f, -46f, 2.8f); // descending north
        Ramp(ramps, 4f, 0f, 28f, 4f, 3f, 37.5f, 3.4f);
        Ramp(ramps, 9f, 3f, 42f, 14.5f, 6f, 42f, 3.2f);

        // Three independent, bidirectional portal pairs across the level.
        // Their floor apertures are spread across different islands.
        ScenePortalPair[] pairs =
        {
            new(
                new ScenePortal(new Vector3(0f, 0f, 3.5f), Vector3.UnitY, 2.36f),
                new ScenePortal(new Vector3(39f, 3.8f, -5f), Vector3.UnitZ, 2.36f)),
            new(
                new ScenePortal(new Vector3(30f, 0f, -12f), Vector3.UnitY, 2.36f),
                new ScenePortal(new Vector3(-44f, 6.5f, -16f), Vector3.UnitZ, 2.36f)),
            new(
                new ScenePortal(new Vector3(0f, 0f, -39f), Vector3.UnitY, 2.36f),
                new ScenePortal(new Vector3(19f, 7f, 42f), Vector3.UnitX, 2.36f))
        };

        return new SceneDefinition(
            size: 180f, tileSize: 2f, spawn: new Vector2(0f, 7f),
            boxes: boxes.ToArray(), portalPairs: pairs, ramps: ramps.ToArray(), baseSize: 50f);
    }

    private static void Deck(List<SceneBox> boxes, float x, float y, float z, float sx, float sz)
    {
        boxes.Add(new SceneBox(new Vector3(x, y - .24f, z), new Vector3(sx, .48f, sz), Surface.Deck));
    }

    private static void Tower(List<SceneBox> boxes, float x, float topY, float z, float sx, float sz)
    {
        // A deck with a supporting column makes the elevation obvious.
        Deck(boxes, x, topY, z, sx, sz);
        boxes.Add(new SceneBox(new Vector3(x, (topY - .48f) / 2f, z),
            new Vector3(sx * .7f, topY - .48f, sz * .7f), Surface.Steel));
    }

    private static void Ramp(List<SceneRamp> ramps, float x0, float y0, float z0,
        float x1, float y1, float z1, float width)
    {
        ramps.Add(new SceneRamp(
            new Vector3(x0, y0, z0), new Vector3(x1, y1, z1), width, .32f, Surface.Edge));
    }
}
