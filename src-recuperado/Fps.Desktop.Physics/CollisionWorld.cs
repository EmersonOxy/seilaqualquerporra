using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Physics;

internal sealed class CollisionWorld
{
	private readonly record struct Box(Vector3 Min, Vector3 Max);

	private readonly Box[] solids;

	private readonly float halfSize;

	public ScenePortalPair? Portals { get; }

	public CollisionWorld(SceneDefinition scene)
	{
		halfSize = scene.Size / 2f;
		Portals = scene.Portals;
		List<Box> list = new List<Box>();
		ReadOnlySpan<SceneBox> boxes = scene.Boxes;
		for (int i = 0; i < boxes.Length; i++)
		{
			SceneBox sceneBox = boxes[i];
			if (sceneBox.Solid)
			{
				list.Add(new Box(sceneBox.Center - sceneBox.Size / 2f, sceneBox.Center + sceneBox.Size / 2f));
			}
		}
		solids = list.ToArray();
	}

	public bool Fits(Vector2 feet, float height, float radius, float elevation = 0f)
	{
		float num = halfSize - radius;
		if (MathF.Abs(feet.X) > num || MathF.Abs(feet.Y) > num || height < 2f * radius)
		{
			return false;
		}
		Box[] array = solids;
		for (int i = 0; i < array.Length; i++)
		{
			Box box = array[i];
			float num2 = MathF.Max(0f, MathF.Max(box.Min.X - feet.X, feet.X - box.Max.X));
			float num3 = MathF.Max(0f, MathF.Max(box.Min.Y - (elevation + height - radius), elevation + radius - box.Max.Y));
			float num4 = MathF.Max(0f, MathF.Max(box.Min.Z - feet.Y, feet.Y - box.Max.Z));
			if (num2 * num2 + num3 * num3 + num4 * num4 < radius * radius - 1E-07f)
			{
				return false;
			}
		}
		ScenePortalPair? portals = Portals;
		if (portals.HasValue)
		{
			ScenePortalPair valueOrDefault = portals.GetValueOrDefault();
			Vector3 lower = new Vector3(feet.X, elevation + radius, feet.Y);
			Vector3 upper = new Vector3(feet.X, elevation + height - radius, feet.Y);
			if (!BlocksFrame(valueOrDefault.First, lower, upper, radius))
			{
				return !BlocksFrame(valueOrDefault.Second, lower, upper, radius);
			}
			return false;
		}
		return true;
	}

	private static bool BlocksFrame(ScenePortal portal, Vector3 lower, Vector3 upper, float radius)
	{
		if (portal.InFloor || !OverlapsSlab(portal.SignedDistance(lower), portal.SignedDistance(upper), 0.12f + radius))
		{
			return false;
		}
		float halfExtent = portal.Radius + 0.34f + radius;
		Vector3 vector = lower - portal.Center;
		Vector3 vector2 = upper - portal.Center;
		Vector3 tangent = portal.Tangent;
		Vector3 bitangent = portal.Bitangent;
		if (OverlapsSlab(Vector3.Dot(vector, tangent), Vector3.Dot(vector2, tangent), halfExtent) && OverlapsSlab(Vector3.Dot(vector, bitangent), Vector3.Dot(vector2, bitangent), halfExtent))
		{
			return !portal.FitsOpening(lower, upper, radius);
		}
		return false;
	}

	private static bool OverlapsSlab(float a, float b, float halfExtent)
	{
		if (MathF.Min(a, b) < halfExtent)
		{
			return MathF.Max(a, b) > 0f - halfExtent;
		}
		return false;
	}

	public Vector2 Move(Vector2 feet, Vector2 delta, float height, float radius, float elevation)
	{
		ScenePortal? scenePortal = ((!(elevation < 0f)) ? ((ScenePortal?)null) : Portals?.FloorOpeningAt(feet, radius - 0.0001f));
		feet = MoveAxis(feet, new Vector2(delta.X, 0f), height, radius, elevation);
		feet = MoveAxis(feet, new Vector2(0f, delta.Y), height, radius, elevation);
		if (scenePortal.HasValue)
		{
			ScenePortal valueOrDefault = scenePortal.GetValueOrDefault();
			if (!valueOrDefault.Contains(feet, radius))
			{
				feet = valueOrDefault.FloorCenter + Vector2.Normalize(feet - valueOrDefault.FloorCenter) * (valueOrDefault.Radius - radius);
			}
		}
		return feet;
	}

	public float MoveVertical(Vector2 feet, float elevation, float target, float height, float radius)
	{
		if (target < 0f && !(Portals?.FloorOpeningAt(feet, radius - 0.0001f)).HasValue)
		{
			target = 0f;
		}
		if (Fits(feet, height, radius, target))
		{
			return target;
		}
		float num = elevation;
		float num2 = target;
		for (int i = 0; i < 12; i++)
		{
			float num3 = (num + num2) / 2f;
			if (Fits(feet, height, radius, num3))
			{
				num = num3;
			}
			else
			{
				num2 = num3;
			}
		}
		return num;
	}

	private Vector2 MoveAxis(Vector2 origin, Vector2 delta, float height, float radius, float elevation)
	{
		if (delta == Vector2.Zero)
		{
			return origin;
		}
		float num = halfSize - radius;
		Vector2 vector = Vector2.Clamp(origin + delta, new Vector2(0f - num), new Vector2(num));
		if (Fits(vector, height, radius, elevation))
		{
			return vector;
		}
		float num2 = 0f;
		float num3 = 1f;
		for (int i = 0; i < 12; i++)
		{
			float num4 = (num2 + num3) / 2f;
			if (Fits(Vector2.Lerp(origin, vector, num4), height, radius, elevation))
			{
				num2 = num4;
			}
			else
			{
				num3 = num4;
			}
		}
		return Vector2.Lerp(origin, vector, num2);
	}
}
