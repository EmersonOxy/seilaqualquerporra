using System.Numerics;

namespace Fps.Desktop.Scenes;

internal readonly record struct ScenePortalPair(ScenePortal First, ScenePortal Second)
{
	public ScenePortal? FloorOpeningAt(Vector2 position, float margin)
	{
		if (!First.InFloor || !First.Contains(position, margin))
		{
			if (!Second.InFloor || !Second.Contains(position, margin))
			{
				return null;
			}
			return Second;
		}
		return First;
	}

	public ScenePortal Other(ScenePortal entry)
	{
		if (!(entry == First))
		{
			return First;
		}
		return Second;
	}
}
