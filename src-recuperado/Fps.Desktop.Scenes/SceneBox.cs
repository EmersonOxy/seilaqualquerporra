using System.Numerics;

namespace Fps.Desktop.Scenes;

internal readonly record struct SceneBox(Vector3 Center, Vector3 Size, Surface Surface, bool Solid = true);
