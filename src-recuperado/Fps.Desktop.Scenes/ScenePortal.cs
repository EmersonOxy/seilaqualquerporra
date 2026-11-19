using System;
using System.Numerics;

namespace Fps.Desktop.Scenes;

internal readonly record struct ScenePortal(Vector3 Center, Vector3 Normal, float Radius)
{
	public bool InFloor
	{
		get
		{
			if (Normal == Vector3.UnitY)
			{
				return Center.Y == 0f;
			}
			return false;
		}
	}

	public Vector2 FloorCenter => new Vector2(Center.X, Center.Z);

	public Vector3 Tangent => Vector3.Normalize(Vector3.Cross((MathF.Abs(Normal.Y) < 0.98f) ? Vector3.UnitY : Vector3.UnitZ, Normal));

	public Vector3 Bitangent => Vector3.Cross(Normal, Tangent);

	public Vector3 DownAlongPlane => Normal.Y * Normal - Vector3.UnitY;

	public bool Contains(Vector2 position, float margin)
	{
		if (margin <= Radius)
		{
			return Vector2.DistanceSquared(position, FloorCenter) <= (Radius - margin) * (Radius - margin);
		}
		return false;
	}

	public float SignedDistance(Vector3 point)
	{
		return Vector3.Dot(point - Center, Normal);
	}

	public Vector3 TunnelPoint(Vector3 side, float fraction)
	{
		return Center + 6f * fraction * (-side + DownAlongPlane * (0.28f * fraction));
	}

	public bool FitsOpening(Vector3 lower, Vector3 upper, float radius)
	{
		float num = Radius - radius;
		if (num >= 0f && ProjectedDistanceSquared(lower) <= num * num)
		{
			return ProjectedDistanceSquared(upper) <= num * num;
		}
		return false;
	}

	private float ProjectedDistanceSquared(Vector3 point)
	{
		return (point - Center - SignedDistance(point) * Normal).LengthSquared();
	}
}
