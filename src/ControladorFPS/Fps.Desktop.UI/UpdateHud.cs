using System;
using Raylib_cs;

namespace Fps.Desktop.UI;

internal static class UpdateHud
{
	public static void Draw(string? status)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (status != null)
		{
			int num = Raylib.MeasureText(status, 16);
			int num2 = Math.Max(12, Raylib.GetScreenWidth() - num - 24);
			Raylib.DrawRectangle(num2 - 6, 12, num + 12, 28, new Color(19, 25, 30, 220));
			Raylib.DrawText(status, num2, 18, 16, Color.White);
		}
	}
}
