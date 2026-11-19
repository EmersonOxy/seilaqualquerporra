using System.Numerics;

namespace Fps.Desktop.Scenes;

// Center + Size + Euler angles (degrees). Gameplay uses the same orientation as rendering.
internal readonly record struct SceneCylinder(
    Vector3 Center, Vector3 Size, Vector3 RotationDegrees, Surface Surface, bool Solid = true);

internal readonly record struct SceneLadder(
    Vector3 Center, Vector3 Size, Vector3 RotationDegrees);

internal readonly record struct SceneWater(
    Vector3 Center, Vector3 Size, Vector3 RotationDegrees);

internal readonly record struct SceneGrass(
    Vector3 Center, Vector3 Size, Vector3 RotationDegrees);
