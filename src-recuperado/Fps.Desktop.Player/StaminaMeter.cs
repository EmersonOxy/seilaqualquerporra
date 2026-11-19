using System;

namespace Fps.Desktop.Player;

internal sealed class StaminaMeter
{
	private float cooldown;

	public float Value { get; private set; } = 100f;

	public bool Exhausted { get; private set; }

	public void Update(bool running, float dt)
	{
		if (running)
		{
			Value = MathF.Max(0f, Value - 24f * dt);
			cooldown = 0.8f;
			if (Value == 0f)
			{
				Exhausted = true;
			}
			return;
		}
		float num = MathF.Max(0f, dt - cooldown);
		cooldown = MathF.Max(0f, cooldown - dt);
		Value = MathF.Min(100f, Value + 20f * num);
		if (Value >= 30f)
		{
			Exhausted = false;
		}
	}
}
