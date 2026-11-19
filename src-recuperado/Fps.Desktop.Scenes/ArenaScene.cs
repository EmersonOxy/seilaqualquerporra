using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Configuration;

namespace Fps.Desktop.Scenes;

internal static class ArenaScene
{
	public static SceneDefinition Create()
	{
		List<SceneBox> list = new List<SceneBox>
		{
			new SceneBox(new Vector3(0f, -0.19f, 0f), new Vector3(50f, 0.36f, 50f), Surface.Deck, Solid: false),
			new SceneBox(new Vector3(GameSettings.World.CanopyCenter.X, 1.36f, GameSettings.World.CanopyCenter.Y), new Vector3(4.8f, 0.24f, 3.4f), Surface.Deck)
		};
		int[] array = new int[2] { -1, 1 };
		foreach (int num in array)
		{
			float z = GameSettings.World.CanopyCenter.Y + (float)num * 1.695f;
			list.Add(new SceneBox(new Vector3(GameSettings.World.CanopyCenter.X, 1.274f, z), new Vector3(4.7000003f, 0.045f, 0.016f), Surface.Edge, Solid: false));
			int[] array2 = new int[2] { -1, 1 };
			foreach (int num2 in array2)
			{
				float x = GameSettings.World.CanopyCenter.X + (float)num2 * 4.54f / 2f;
				float z2 = GameSettings.World.CanopyCenter.Y + (float)num * 3.14f / 2f;
				list.Add(new SceneBox(new Vector3(x, 0.62f, z2), new Vector3(0.26f, 1.24f, 0.26f), Surface.Steel));
			}
		}
		return new SceneDefinition(portals: new ScenePortalPair(new ScenePortal(GameSettings.Portal.First, GameSettings.Portal.FirstNormal, 2.36f), new ScenePortal(GameSettings.Portal.Second, GameSettings.Portal.SecondNormal, 2.36f)), size: 50f, tileSize: 2f, spawn: GameSettings.World.Spawn, boxes: list.ToArray());
	}
}
