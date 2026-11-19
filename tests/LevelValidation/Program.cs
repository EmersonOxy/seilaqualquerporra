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
