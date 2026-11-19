using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Desktop.Player;
using Fps.Desktop.Scenes;
using Raylib_cs;

namespace Fps.Desktop.Rendering;

internal sealed class PortalGeometry
{
	private readonly record struct Face(Vector3 A, Vector3 B, Vector3 C, Color Color);

	private const int Segments = 32;

	private readonly ScenePortal portal;

	private readonly Face[] frame;

	private readonly Face[] frontTunnel;

	private readonly Face[] backTunnel;

	public PortalGeometry(ScenePortal portal)
	{
		this.portal = portal;
		frame = (portal.InFloor ? Array.Empty<Face>() : BuildFrame(portal));
		frontTunnel = BuildTunnel(portal, portal.Normal);
		backTunnel = (portal.InFloor ? Array.Empty<Face>() : BuildTunnel(portal, -portal.Normal));
	}

	public void Draw(PortalTravelView? travel)
	{
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		Face[] array;
		if (travel.HasValue)
		{
			PortalTravelView valueOrDefault = travel.GetValueOrDefault();
			if (valueOrDefault.Opening == portal)
			{
				array = ((Vector3.Dot(valueOrDefault.Side, portal.Normal) >= 0f) ? frontTunnel : backTunnel);
				goto IL_0058;
			}
		}
		array = Array.Empty<Face>();
		goto IL_0058;
		IL_0058:
		Face[] array2 = array;
		if (frame.Length != 0 || array2.Length != 0)
		{
			Rlgl.DrawRenderBatchActive();
			Rlgl.DisableBackfaceCulling();
			Face[] array3 = frame;
			for (int i = 0; i < array3.Length; i++)
			{
				Face face = array3[i];
				Raylib.DrawTriangle3D(face.A, face.B, face.C, face.Color);
			}
			array3 = array2;
			for (int i = 0; i < array3.Length; i++)
			{
				Face face2 = array3[i];
				Raylib.DrawTriangle3D(face2.A, face2.B, face2.C, face2.Color);
			}
			Rlgl.DrawRenderBatchActive();
			Rlgl.EnableBackfaceCulling();
		}
	}

	private static Face[] BuildFrame(ScenePortal portal)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		List<Face> list = new List<Face>();
		float num = portal.Radius + 0.34f;
		Vector3 vector = portal.Normal * 0.12f;
		Vector3 tangent = portal.Tangent;
		Vector3 bitangent = portal.Bitangent;
		Color color = new Color(52, 63, 73, 255);
		Color color2 = new Color(29, 36, 43, 255);
		for (int i = 0; i < 32; i++)
		{
			Vector2 vector2 = CirclePoint(i);
			Vector2 vector3 = CirclePoint(i + 1);
			Vector3 vector4 = tangent * vector2.X + bitangent * vector2.Y;
			Vector3 vector5 = tangent * vector3.X + bitangent * vector3.Y;
			Vector3 vector6 = portal.Center + vector4 * portal.Radius;
			Vector3 vector7 = portal.Center + vector5 * portal.Radius;
			Vector3 vector8 = portal.Center + vector4 * (num / MathF.Max(MathF.Abs(vector2.X), MathF.Abs(vector2.Y)));
			Vector3 vector9 = portal.Center + vector5 * (num / MathF.Max(MathF.Abs(vector3.X), MathF.Abs(vector3.Y)));
			AddQuad(list, vector6 + vector, vector7 + vector, vector9 + vector, vector8 + vector, color);
			AddQuad(list, vector6 - vector, vector7 - vector, vector9 - vector, vector8 - vector, color);
			AddQuad(list, vector8 + vector, vector9 + vector, vector9 - vector, vector8 - vector, color2);
			AddQuad(list, vector6 + vector, vector7 + vector, vector7 - vector, vector6 - vector, color2);
		}
		return list.ToArray();
	}

	private static Face[] BuildTunnel(ScenePortal portal, Vector3 side)
	{
		//IL_016c: Unknown result type (might be due to invalid IL or missing references)
		//IL_019e: Unknown result type (might be due to invalid IL or missing references)
		List<Face> list = new List<Face>();
		Vector3 tangent = portal.Tangent;
		Vector3 bitangent = portal.Bitangent;
		for (int i = 0; i < 12; i++)
		{
			float num = (float)i / 12f;
			float num2 = (float)(i + 1) / 12f;
			Vector3 vector = portal.TunnelPoint(side, num);
			Vector3 vector2 = portal.TunnelPoint(side, num2);
			float num3 = portal.Radius * (1f - num * 0.22f);
			float num4 = portal.Radius * (1f - num2 * 0.22f);
			for (int j = 0; j < 32; j++)
			{
				Vector2 vector3 = CirclePoint(j);
				Vector2 vector4 = CirclePoint(j + 1);
				Vector3 vector5 = vector3.X * tangent + vector3.Y * bitangent;
				Vector3 vector6 = vector4.X * tangent + vector4.Y * bitangent;
				int num5 = (int)(((i % 2 == 0) ? 83f : 64f) * (1f - num) * (1f - num));
				num5 = (int)((float)num5 * (0.78f + 0.22f * MathF.Abs(vector3.Y)));
				AddQuad(list, vector + vector5 * num3, vector + vector6 * num3, vector2 + vector6 * num4, vector2 + vector5 * num4, new Color(num5, num5, num5 + 6, 255));
				if (i == 11)
				{
					list.Add(new Face(vector2, vector2 + vector5 * num4, vector2 + vector6 * num4, Color.Black));
				}
			}
		}
		return list.ToArray();
	}

	private static Vector2 CirclePoint(int index)
	{
		(float Sin, float Cos) tuple = MathF.SinCos((float)Math.PI * 2f * (float)index / 32f);
		var (y, _) = tuple;
		return new Vector2(tuple.Cos, y);
	}

	private static void AddQuad(List<Face> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		faces.Add(new Face(a, b, c, color));
		faces.Add(new Face(a, c, d, color));
	}
}
