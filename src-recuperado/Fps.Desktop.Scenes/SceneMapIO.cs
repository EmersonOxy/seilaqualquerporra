using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fps.Desktop.Scenes;

internal enum MapObjectKind { Box, Ramp, Cylinder, Stairs, Ladder }

internal sealed class MapObject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Objeto";
    public MapObjectKind Kind { get; set; }
    // All object origins are at the center of their geometry.
    public Vector3 Position { get; set; }
    // Box: XYZ; Ramp/Stairs: width/vertical rise/horizontal run; Ladder: width/height/thickness.
    public Vector3 Size { get; set; } = Vector3.One;
    // Angles in degrees. Ramp and stairs use yaw; boxes and cylinders support all three axes.
    public Vector3 Rotation { get; set; }
    public Surface Surface { get; set; } = Surface.Deck;
    public bool Solid { get; set; } = true;
    public bool Visible { get; set; } = true;
}

internal sealed class MapPortal
{
    public Vector3 FirstCenter { get; set; }
    public Vector3 FirstNormal { get; set; } = Vector3.UnitY;
    public float FirstRadius { get; set; } = 2.36f;
    public Vector3 SecondCenter { get; set; }
    public Vector3 SecondNormal { get; set; } = Vector3.UnitZ;
    public float SecondRadius { get; set; } = 2.36f;

    public ScenePortalPair Build() => new(
        new ScenePortal(FirstCenter, Vector3.Normalize(FirstNormal), FirstRadius),
        new ScenePortal(SecondCenter, Vector3.Normalize(SecondNormal), SecondRadius));
}

internal sealed class MapDocument
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "Parkour e Portais";
    public float WorldSize { get; set; } = 180f;
    public float BaseSize { get; set; } = 50f;
    public float TileSize { get; set; } = 2f;
    public Vector2 Spawn { get; set; } = new(0f, 7f);
    public List<MapObject> Objects { get; set; } = new();
    public List<MapPortal> Portals { get; set; } = new();

    public SceneDefinition ToScene()
    {
        List<SceneBox> boxes = new();
        List<SceneRamp> ramps = new();
        List<SceneCylinder> cylinders = new();
        List<SceneLadder> ladders = new();
        foreach (MapObject o in Objects)
        {
            if (!o.Visible) continue;
            switch (o.Kind)
            {
                case MapObjectKind.Box:
                    boxes.Add(new SceneBox(o.Position, o.Size, o.Surface, o.Solid, o.Rotation));
                    break;
                case MapObjectKind.Cylinder:
                    cylinders.Add(new SceneCylinder(o.Position, o.Size, o.Rotation, o.Surface, o.Solid));
                    break;
                case MapObjectKind.Ladder:
                    ladders.Add(new SceneLadder(o.Position, o.Size, o.Rotation));
                    break;
                case MapObjectKind.Ramp:
                {
                    float angle = o.Rotation.Y * MathF.PI / 180f;
                    Vector3 along = new(MathF.Sin(angle), 0f, MathF.Cos(angle));
                    Vector3 start = o.Position - along * (o.Size.Z * .5f) - Vector3.UnitY * (o.Size.Y * .5f);
                    Vector3 end = o.Position + along * (o.Size.Z * .5f) + Vector3.UnitY * (o.Size.Y * .5f);
                    ramps.Add(new SceneRamp(start, end, o.Size.X, .32f, o.Surface));
                    break;
                }
                case MapObjectKind.Stairs:
                {
                    int stepCount = Math.Clamp((int)MathF.Ceiling(o.Size.Y / .22f), 3, 80);
                    float stepHeight = o.Size.Y / stepCount;
                    float stepRun = o.Size.Z / stepCount;
                    float angle = o.Rotation.Y * MathF.PI / 180f;
                    Vector3 along = new(MathF.Sin(angle), 0f, MathF.Cos(angle));
                    float bottomY = o.Position.Y - o.Size.Y * .5f;
                    Vector3 origin = o.Position - along * (o.Size.Z * .5f);
                    for (int i = 0; i < stepCount; i++)
                    {
                        float height = (i + 1) * stepHeight;
                        Vector3 center = origin + along * ((i + .5f) * stepRun);
                        center.Y = bottomY + height * .5f;
                        boxes.Add(new SceneBox(center, new Vector3(o.Size.X, height, stepRun),
                            o.Surface, o.Solid, new Vector3(0f, o.Rotation.Y, 0f)));
                    }
                    break;
                }
            }
        }

        List<ScenePortalPair> portals = new();
        foreach (MapPortal pair in Portals)
            portals.Add(pair.Build());

        return new SceneDefinition(WorldSize, TileSize, Spawn, boxes.ToArray(),
            portals.ToArray(), ramps.ToArray(), BaseSize, cylinders.ToArray(), ladders.ToArray());
    }
}

internal static class SceneMapIO
{
    public const string FileName = "map.json";
    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, FileName);

    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static MapDocument CreateDefault()
    {
        SceneDefinition scene = ArenaScene.Create();
        MapDocument data = new()
        {
            Name = "Parkour e Portais",
            WorldSize = scene.Size,
            BaseSize = scene.BaseSize,
            TileSize = scene.TileSize,
            Spawn = scene.Spawn
        };
        int boxIndex = 0;
        foreach (SceneBox b in scene.Boxes)
        {
            data.Objects.Add(new MapObject
            {
                Name = "Bloco " + ++boxIndex,
                Kind = MapObjectKind.Box,
                Position = b.Center,
                Size = b.Size,
                Rotation = b.RotationDegrees,
                Surface = b.Surface,
                Solid = b.Solid
            });
        }

        int rampIndex = 0;
        foreach (SceneRamp r in scene.Ramps)
        {
            Vector3 delta = r.End - r.Start;
            data.Objects.Add(new MapObject
            {
                Name = "Rampa " + ++rampIndex,
                Kind = MapObjectKind.Ramp,
                Position = (r.Start + r.End) * .5f,
                Size = new Vector3(r.Width, delta.Y, r.Run),
                Rotation = new Vector3(0f, MathF.Atan2(delta.X, delta.Z) * (180f / MathF.PI), 0f),
                Surface = r.Surface
            });
        }
        // Built-in parkour accessories: every step is a real solid block.
        data.Objects.Add(new MapObject
        {
            Name = "Escada leste", Kind = MapObjectKind.Stairs,
            Position = new Vector3(31.4f, 1.4f, -8.9f),
            Size = new Vector3(2.3f, 2.8f, 6.2f),
            Rotation = new Vector3(0f, 90f, 0f),
            Surface = Surface.Edge
        });
        data.Objects.Add(new MapObject
        {
            Name = "Escada sul", Kind = MapObjectKind.Stairs,
            Position = new Vector3(11.25f, 4.5f, 45.9f),
            Size = new Vector3(2.7f, 3f, 6.5f),
            Rotation = new Vector3(0f, 90f, 0f),
            Surface = Surface.Edge
        });
        data.Objects.Add(new MapObject
        {
            Name = "Escada vertical leste", Kind = MapObjectKind.Ladder,
            Position = new Vector3(41.6f, 1.55f, -.15f),
            Size = new Vector3(1.4f, 3.1f, .3f),
            Surface = Surface.Steel, Solid = false
        });
        data.Objects.Add(new MapObject
        {
            Name = "Escada vertical oeste", Kind = MapObjectKind.Ladder,
            Position = new Vector3(-42.5f, 2.9f, -10.75f),
            Size = new Vector3(1.4f, 5.8f, .3f),
            Surface = Surface.Steel, Solid = false
        });
        foreach (ScenePortalPair pair in scene.PortalPairs)
        {
            data.Portals.Add(new MapPortal
            {
                FirstCenter = pair.First.Center,
                FirstNormal = pair.First.Normal,
                FirstRadius = pair.First.Radius,
                SecondCenter = pair.Second.Center,
                SecondNormal = pair.Second.Normal,
                SecondRadius = pair.Second.Radius
            });
        }
        return data;
    }

    public static MapDocument Load(string path)
    {
        string json = File.ReadAllText(path);
        MapDocument data = JsonSerializer.Deserialize<MapDocument>(json, Options)
            ?? throw new InvalidDataException("Mapa vazio ou invalido.");
        Validate(data);
        return data;
    }

    public static MapDocument LoadOrDefault(string path) =>
        File.Exists(path) ? Load(path) : CreateDefault();

    public static void Save(MapDocument data, string path)
    {
        Validate(data);
        string temp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
        File.Move(temp, path, true);
    }

    private static void Validate(MapDocument data)
    {
        if (data.Version != 1)
            throw new InvalidDataException("Versao de mapa nao suportada: " + data.Version);
        if (data.Objects == null || data.Portals == null || data.Objects.Count > 20000)
            throw new InvalidDataException("Lista de objetos invalida.");
        foreach (MapObject o in data.Objects)
        {
            if (!Finite(o.Position) || !Finite(o.Size) || !Finite(o.Rotation) ||
                o.Size.X < .05f || o.Size.Y < .05f || o.Size.Z < .05f ||
                o.Size.X > 500f || o.Size.Y > 500f || o.Size.Z > 500f)
                throw new InvalidDataException("Dimensoes invalidas para: " + o.Name);
        }
        foreach (MapPortal p in data.Portals)
        {
            if (!Finite(p.FirstCenter) || !Finite(p.SecondCenter) ||
                !Finite(p.FirstNormal) || !Finite(p.SecondNormal) ||
                p.FirstNormal.LengthSquared() < .1f || p.SecondNormal.LengthSquared() < .1f ||
                p.FirstRadius < .5f || p.SecondRadius < .5f)
                throw new InvalidDataException("Portal invalido.");
        }
    }

    private static bool Finite(Vector3 v) =>
        float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
