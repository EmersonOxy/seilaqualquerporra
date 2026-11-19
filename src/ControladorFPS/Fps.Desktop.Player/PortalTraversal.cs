using System;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Player;

internal sealed class PortalTraversal(ScenePortal entry, Vector3 entrySide, Vector3 startEye, ScenePortal exit, Vector3 endEye, Vector3 floorExitDirection)
{
	private const float Transfer = 0.55f;

	private float elapsed;

	public ScenePortal Entry { get; } = entry;

	public ScenePortal Exit { get; } = exit;

	public Vector3 EndEye { get; } = endEye;

	public Vector3 FloorExitDirection { get; } = floorExitDirection;

	public PortalTravelView View
	{
		get
		{
			float num = Math.Clamp(elapsed / 1.55f, 0f, 1f);
			float darkness = Smooth(0.22f, 0.52f, num) * (1f - Smooth(0.6f, 0.98f, num));
			if (num < 0.55f)
			{
				float num2 = num / 0.55f;
				float num3 = num2 * (0.32f + 0.68f * num2);
				Vector3 eye = entry.TunnelPoint(entrySide, num3) + (startEye - entry.Center) * (1f - num3);
				Vector3 direction = Vector3.Normalize(-entrySide + entry.DownAlongPlane * (0.56f * num3));
				return new PortalTravelView(eye, direction, entry, entrySide, darkness);
			}
			float num4 = (num - 0.55f) / 0.45f;
			Vector3 eye2 = Exit.TunnelPoint(Exit.Normal, 1f - num4) + (EndEye - Exit.Center) * num4;
			Vector3 direction2 = (Exit.InFloor ? Vector3.Normalize(FloorExitDirection + Vector3.UnitY * (3.1f * (1f - Smooth(0.52f, 1f, num4)))) : Vector3.Normalize(Exit.Normal - Exit.DownAlongPlane * (0.56f * (1f - num4))));
			return new PortalTravelView(eye2, direction2, Exit, Exit.Normal, darkness);
		}
	}

	public bool Advance(float dt)
	{
		elapsed += dt;
		return elapsed >= 1.55f;
	}

	private static float Smooth(float from, float to, float value)
	{
		float num = Math.Clamp((value - from) / (to - from), 0f, 1f);
		return num * num * (3f - 2f * num);
	}
}
