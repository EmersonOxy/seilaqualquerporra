using System.Numerics;

namespace Fps.Desktop.Scenes;

// Fully data-driven collectible positions shared by editor and game.
internal readonly record struct SceneCollectible(string Name, Vector3 Position, int RequiredPortal = -1);
