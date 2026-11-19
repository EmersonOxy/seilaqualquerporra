using System;
using System.Numerics;
using Fps.Desktop.Gameplay;
using Fps.Desktop.Physics;
using Fps.Desktop.Scenes;

var scene = ArenaScene.Create();
var world = new CollisionWorld(scene);

void Assert(bool condition, string reason)
{
    if (!condition) throw new Exception("Level validation failed: " + reason);
}
Assert(scene.Size > scene.BaseSize, "No outer world");
Assert(scene.Ramps.Length >= 6, "Not enough ramps");
Assert(scene.Boxes.Length >= 15, "Not enough platforms or towers");
Assert(scene.PortalPairs.Length == 3, "Expected three portal pairs");
Assert(world.Fits(scene.Spawn, 1.7f, .32f), "Spawn blocked");

Vector2[] floorHoles = { new(0f, 3.5f), new(30f, -12f), new(0f, -39f) };
foreach (Vector2 center in floorHoles)
{
    float down = world.MoveVertical(center, 0f, -.22f, 1.7f, .32f);
    Assert(down < -.2f, "Floor portal does not physically open: " + center);
}
Assert(MathF.Abs(world.MoveVertical(new Vector2(12f, 12f), 0f, -.2f, 1.7f, .32f)) < .001f,
    "Main floor should catch falling player");
Assert(MathF.Abs(world.MoveVertical(new Vector2(70f, 60f), 0f, -1f, 1.7f, .32f) + 1f) < .001f,
    "Void outside platforms should not have invisible floor");

float elevation = 0f;
var progressed = world.Move(new Vector2(26f, -5f), new Vector2(.1f, 0f),
    1.7f, .32f, ref elevation, true);
Assert(progressed.X > 26f && elevation > .02f, "East ramp must be climbable");

var game = new CollectibleManager(scene);
Assert(game.TotalCount >= 12, "Not enough pickups");
Assert(game.UsedPortalPairs == 0, "Portals must start unused");
foreach (ScenePortalPair pair in scene.PortalPairs)
    game.NotifyPortalTraversed(pair.First, pair.Second);
Assert(game.UsedPortalPairs == 3, "Traversal of every pair must count");

Console.WriteLine($"VALIDATED: {scene.Boxes.Length} objects, {scene.Ramps.Length} ramps, " +
    $"{scene.PortalPairs.Length} bidirectional portal pairs, {game.TotalCount} pickups.");


var editable = SceneMapIO.CreateDefault();
Assert(editable.Objects.Count >= 30, "Current map must be imported into editor");
Assert(editable.Objects.Exists(o => o.Kind == MapObjectKind.Stairs), "No stair object");
Assert(editable.Objects.Exists(o => o.Kind == MapObjectKind.Ladder), "No vertical ladder");
editable.Objects.Add(new MapObject {
    Name = "Cilindro teste", Kind = MapObjectKind.Cylinder,
    Position = new Vector3(60f, 1f, 55f), Size = new Vector3(2f, 2f, 2f)
});
string jsonPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "denffy-map-validation-" + Guid.NewGuid() + ".json");
try
{
    SceneMapIO.Save(editable, jsonPath);
    var opened = SceneMapIO.Load(jsonPath);
    Assert(opened.Objects.Count == editable.Objects.Count, "JSON loses map entities");
    Assert(opened.Objects[^1].Kind == MapObjectKind.Cylinder, "Cylinder changed while saving");
    Assert(opened.Objects[^1].Position == new Vector3(60f, 1f, 55f),
        "Position changed after save/reload");
    Assert(opened.Portals.Count == 3, "Portals were lost in JSON");
    var walkable = new CollisionWorld(opened.ToScene());

    Vector2 east = new(25.8f, -5f);
    float eastY = 0f;
    for (int i = 0; i < 65; i++)
    {
        Vector2 before = east;
        east = walkable.Move(east, new Vector2(.14f, 0f), 1.7f, .32f, ref eastY, true);
        Assert(east.X > before.X + .08f, "Ramp movement blocked without jumping near x=" + east.X);
    }
    Assert(eastY > 2.4f, "Walking up slope failed: final height " + eastY);

    // Walk UP a staircase one physical tread at a time, without jumping.
    Vector2 stair = new(28.0f, -8.9f);
    float stepY = 0f;
    int raised = 0;
    for (int i = 0; i < 46; i++)
    {
        Vector2 before = stair;
        float wasY = stepY;
        stair = walkable.Move(stair, new Vector2(.14f, 0f), 1.7f, .32f, ref stepY, true);
        Assert(stair.X > before.X + .07f, "Stair collision blocked step at x=" + stair.X);
        if (stepY > wasY + .05f) raised++;
    }
    Assert(raised >= 7, "Stairs should have discrete physical risers, got " + raised);
    Assert(stepY > 2.5f, "Cannot reach staircase upper landing");

    // Vertical ladder is detectable at every height.
    Assert(walkable.TryGetLadder(new Vector2(41.6f, -.15f), 0f, 1.7f, out _),
        "Ladder base cannot be entered");
    Assert(walkable.TryGetLadder(new Vector2(41.6f, -.15f), 2f, 1.7f, out _),
        "Ladder top cannot be entered");

    // Add a cylinder, ensure its collider is active and the empty area is still passable.
    Assert(!walkable.Fits(new Vector2(60f, 55f), 1.7f, .32f, .3f),
        "Cylinder does not collide");
    Assert(walkable.Fits(new Vector2(64f, 55f), 1.7f, .32f, 0f),
        "Cylinder collider too wide");
    Console.WriteLine("MAP EDITOR + PHYSICS VALIDATED: ramps, stair treads, ladder, cylinder and JSON.");
}
finally
{
    if (System.IO.File.Exists(jsonPath)) System.IO.File.Delete(jsonPath);
}
