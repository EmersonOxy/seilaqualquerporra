using System;
using System.Numerics;
using Raylib_cs;

namespace Fps.Desktop.Input;

internal sealed class DesktopInput
{
	[Flags]
	private enum Buttons
	{
		None = 0,
		Forward = 1,
		Back = 2,
		Left = 4,
		Right = 8,
		Sprint = 0x10,
		Slow = 0x20,
		CrouchHeld = 0x40,
		ToggleCrouch = 0x80
	}

	private Buttons blockedUntilRelease;

	public bool Captured { get; private set; }

	public bool UpdatePressed { get; private set; }

	public bool OutlinePressed { get; private set; }

	public bool SkyPressed { get; private set; }

	public InputState Poll()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Invalid comparison between Unknown and I4
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Invalid comparison between Unknown and I4
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Invalid comparison between Unknown and I4
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Invalid comparison between Unknown and I4
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Invalid comparison between Unknown and I4
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Invalid comparison between Unknown and I4
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e8: Unknown result type (might be due to invalid IL or missing references)
		bool flag = false;
		bool flag2 = false;
		bool flag3 = false;
		UpdatePressed = false;
		OutlinePressed = false;
		SkyPressed = false;
		KeyboardKey val = (KeyboardKey)Raylib.GetKeyPressed();
		while ((int)val != 0)
		{
			flag |= (int)val == 256;
			flag2 ^= (int)val == 67;
			flag3 |= (int)val == 32;
			UpdatePressed |= (int)val == 295;
			OutlinePressed ^= (int)val == 296;
			SkyPressed |= (int)val == 297;
			val = (KeyboardKey)Raylib.GetKeyPressed();
		}
		bool flag4 = CBool.op_Implicit(Raylib.IsWindowFocused());
		if (!flag4)
		{
			bool flag5 = (SkyPressed = false);
			bool updatePressed = (OutlinePressed = flag5);
			UpdatePressed = updatePressed;
		}
		if (!flag4 | flag)
		{
			if (Captured)
			{
				Raylib.EnableCursor();
			}
			Captured = false;
			return default;
		}
		if (!Captured)
		{
			if (CBool.op_Implicit(Raylib.IsMouseButtonPressed((MouseButton)0)))
			{
				blockedUntilRelease = ReadButtons();
				Raylib.DisableCursor();
				Captured = true;
			}
			return default;
		}
		Buttons buttons = ReadButtons();
		blockedUntilRelease &= buttons;
		buttons &= ~blockedUntilRelease;
		return new InputState(new Vector2(Axis(buttons, Buttons.Right, Buttons.Left), Axis(buttons, Buttons.Forward, Buttons.Back)), Raylib.GetMouseDelta(), Has(buttons, Buttons.Sprint), Has(buttons, Buttons.Slow), Has(buttons, Buttons.CrouchHeld), flag2 && !Has(blockedUntilRelease, Buttons.ToggleCrouch), flag3);
	}

	private static float Axis(Buttons buttons, Buttons positive, Buttons negative)
	{
		return (Has(buttons, positive) ? 1f : 0f) - (Has(buttons, negative) ? 1f : 0f);
	}

	private static bool Has(Buttons buttons, Buttons button)
	{
		return (buttons & button) != 0;
	}

	private static Buttons ReadButtons()
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		Buttons buttons = Buttons.None;
		if (CBool.op_Implicit(Raylib.IsKeyDown((KeyboardKey)87)))
		{
			buttons |= Buttons.Forward;
		}
		if (CBool.op_Implicit(Raylib.IsKeyDown((KeyboardKey)83)))
		{
			buttons |= Buttons.Back;
		}
		if (CBool.op_Implicit(Raylib.IsKeyDown((KeyboardKey)65)))
		{
			buttons |= Buttons.Left;
		}
		if (CBool.op_Implicit(Raylib.IsKeyDown((KeyboardKey)68)))
		{
			buttons |= Buttons.Right;
		}
		if (Either((KeyboardKey)340, (KeyboardKey)344))
		{
			buttons |= Buttons.Sprint;
		}
		if (Either((KeyboardKey)342, (KeyboardKey)346))
		{
			buttons |= Buttons.Slow;
		}
		if (Either((KeyboardKey)341, (KeyboardKey)345))
		{
			buttons |= Buttons.CrouchHeld;
		}
		if (CBool.op_Implicit(Raylib.IsKeyDown((KeyboardKey)67)))
		{
			buttons |= Buttons.ToggleCrouch;
		}
		return buttons;
	}

	private static bool Either(KeyboardKey first, KeyboardKey second)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (!CBool.op_Implicit(Raylib.IsKeyDown(first)))
		{
			return CBool.op_Implicit(Raylib.IsKeyDown(second));
		}
		return true;
	}
}
