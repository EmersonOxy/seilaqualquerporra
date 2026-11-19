using System;
using System.Numerics;

namespace Fps.Desktop.Player;

internal sealed class FpsCamera
{
	private float pitch = -0.035f;

	private float roll;

	private float phase;

	private float bob;

	public float Yaw { get; private set; }

	public void Look(Vector2 mouse)
	{
		Yaw = (Yaw - mouse.X * 0.0022f) % ((float)Math.PI * 2f);
		pitch = Math.Clamp(pitch - mouse.Y * 0.0022f, -1.5393804f, 1.5393804f);
	}

	public CameraPose Update(PlayerController player, float mouseX, float dt)
	{
		float target = Math.Clamp(((dt > 0f) ? (mouseX * 0.0022f / dt) : 0f) * 0.012f, -0.035f, 0.035f);
		roll = MathEx.Damp(roll, target, (mouseX == 0f) ? 10f : 17f, dt);
		if (player.Running)
		{
			phase = (phase + player.Speed * 0.46f * ((float)Math.PI * 2f) * dt) % ((float)Math.PI * 2f);
		}
		float target2 = (player.Running ? (0.032f * player.Speed / 6.3f) : 0f);
		bob = MathEx.Damp(bob, target2, 7f, dt);
		float num = player.EyeHeight + MathF.Sin(phase) * bob;
		Vector3 eye = new Vector3(player.Feet.X, player.Elevation + num, player.Feet.Y);
		PortalTravelView? portalView = player.PortalView;
		if (portalView.HasValue)
		{
			PortalTravelView valueOrDefault = portalView.GetValueOrDefault();
			eye = valueOrDefault.Eye;
			Vector3 direction = valueOrDefault.Direction;
			float num2 = Math.Clamp(MathF.Asin(Math.Clamp(direction.Y, -1f, 1f)), -1.32f, 1.32f);
			pitch = ((valueOrDefault.Darkness >= 0.999f) ? num2 : MathEx.Damp(pitch, num2, 9f, dt));
			if (direction.X * direction.X + direction.Z * direction.Z > 0.0001f)
			{
				float num3 = MathF.IEEERemainder(MathF.Atan2(0f - direction.X, 0f - direction.Z) - Yaw, (float)Math.PI * 2f);
				Yaw += ((valueOrDefault.Darkness >= 0.999f) ? num3 : MathEx.Damp(0f, num3, 9f, dt));
			}
		}
		(float Sin, float Cos) tuple = MathF.SinCos(Yaw);
		float item = tuple.Sin;
		float item2 = tuple.Cos;
		(float Sin, float Cos) tuple2 = MathF.SinCos(pitch);
		float item3 = tuple2.Sin;
		float item4 = tuple2.Cos;
		Vector3 vector = new Vector3((0f - item) * item4, item3, (0f - item2) * item4);
		Vector3 vector2 = new Vector3(item2, 0f, 0f - item);
		Vector3 vector3 = Vector3.Cross(vector2, vector);
		var (num4, num5) = MathF.SinCos(roll);
		return new CameraPose(eye, vector, vector3 * num5 + vector2 * num4, 70f);
	}
}
