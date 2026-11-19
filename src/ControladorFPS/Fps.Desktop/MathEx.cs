using System;

namespace Fps.Desktop;

internal static class MathEx
{
	public static float Approach(float value, float target, float amount)
	{
		float num = target - value;
		if (!(MathF.Abs(num) <= amount))
		{
			return value + MathF.CopySign(amount, num);
		}
		return target;
	}

	public static float Damp(float value, float target, float response, float dt)
	{
		return target + (value - target) * MathF.Exp((0f - response) * dt);
	}
}
