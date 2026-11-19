using System;
using System.Numerics;

namespace Fps.Desktop.Scenes;

internal sealed class SceneDefinition(float size, float tileSize, Vector2 spawn, SceneBox[] boxes, ScenePortalPair? portals = null)
{
	public float Size { get; } = size;

	public float TileSize { get; } = tileSize;

	public Vector2 Spawn { get; } = spawn;

	public ReadOnlySpan<SceneBox> Boxes => boxes;

	public ScenePortalPair? Portals { get; } = portals;
}
