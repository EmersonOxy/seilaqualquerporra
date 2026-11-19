using System;
using System.Numerics;
using Raylib_cs;

namespace Fps.Desktop.UI;

internal sealed class Hud : IDisposable
{
	private readonly Texture2D vignette;

	private float displayedStamina = 100f;

	public Hud()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		Image val = Raylib.GenImageColor(256, 256, new Color(0, 0, 0, 0));
		for (int i = 0; i < 256; i++)
		{
			for (int j = 0; j < 256; j++)
			{
				float num = Math.Clamp(((new Vector2(j, i) * (2f / 255f) - Vector2.One).Length() - 0.56f) / (MathF.Sqrt(2f) - 0.56f), 0f, 1f);
				float num2 = num * num * (3f - 2f * num);
				Raylib.ImageDrawPixel(ref val, j, i, new Color(0, 0, 0, (int)(255f * num2 * 0.17f)));
			}
		}
		vignette = Raylib.LoadTextureFromImage(val);
		Raylib.UnloadImage(val);
		Raylib.SetTextureFilter(vignette, (TextureFilter)1);
	}

	public void Draw(float stamina, float dt)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		int screenWidth = Raylib.GetScreenWidth();
		int screenHeight = Raylib.GetScreenHeight();
		Raylib.DrawTexturePro(vignette, new Rectangle(0f, 0f, (float)vignette.Width, (float)vignette.Height), new Rectangle(0f, 0f, (float)screenWidth, (float)screenHeight), Vector2.Zero, 0f, Color.White);
		Raylib.DrawCircleV(new Vector2((float)screenWidth / 2f, (float)screenHeight / 2f), 2f, Color.White);
		displayedStamina = ((stamina <= displayedStamina) ? stamina : MathEx.Damp(displayedStamina, stamina, 14f, dt));
		if (MathF.Abs(displayedStamina - stamina) < 0.01f)
		{
			displayedStamina = stamina;
		}
		float num = Math.Clamp((float)screenWidth * 0.04f, 20f, 40f);
		float num2 = Math.Clamp((float)screenWidth * 0.19f, 108f, 210f);
		Rectangle val = new Rectangle(num, (float)screenHeight - num - 5f, num2, 5f);
		Raylib.DrawRectangleRec(val, new Color(0, 0, 0, 118));
		val.Width *= displayedStamina / 100f;
		Raylib.DrawRectangleRec(val, Color.White);
	}

	public void Dispose()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		Raylib.UnloadTexture(vignette);
	}
}
