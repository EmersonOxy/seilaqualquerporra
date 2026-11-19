using System.Numerics;

namespace Fps.Desktop.Configuration;

internal static class GameSettings
{
	public static class World
	{
		public const float Size = 50f;

		public const float TileSize = 2f;

		public static readonly Vector2 Spawn = new Vector2(0f, 7f);

		public static readonly Vector2 CanopyCenter = new Vector2(0f, -1.5f);

		public const float CanopyWidth = 4.8f;

		public const float CanopyDepth = 3.4f;

		public const float CanopyClearance = 1.24f;

		public const float CanopyThickness = 0.24f;

		public const float PostWidth = 0.26f;
	}

	public static class Body
	{
		public const float StandingHeight = 1.7f;

		public const float StandingEyeHeight = 1.6f;

		public const float CrouchHeight = 1.1f;

		public const float CrouchEyeHeight = 1f;

		public const float CapsuleRadius = 0.32f;

		public const float PostureSpeed = 3.5f;
	}

	public static class Movement
	{
		public const float WalkSpeed = 3.4f;

		public const float SprintSpeed = 6.3f;

		public const float CrouchSpeed = 2.1f;

		public const float SlowSpeed = 1.45f;

		public const float Acceleration = 27f;

		public const float Deceleration = 34f;

		public const float MaxStep = 1f / 120f;

		public const float MaxFrameTime = 0.25f;

		public const float Gravity = 12f;

		public const float JumpSpeed = 4.8f;

		public const float AirControl = 0.35f;
	}

	public static class Camera
	{
		public const float VerticalFov = 70f;

		public const float MouseSensitivity = 0.0022f;

		public const float PitchLimit = 1.5393804f;

		public const float RollMax = 0.035f;

		public const float RollGain = 0.012f;

		public const float RollResponse = 17f;

		public const float RollReturn = 10f;

		public const float BobAmplitude = 0.032f;

		public const float BobStepsPerMeter = 0.46f;

		public const float BobResponse = 7f;
	}

	public static class Stamina
	{
		public const float Max = 100f;

		public const float Drain = 24f;

		public const float Regen = 20f;

		public const float RegenDelay = 0.8f;

		public const float RestartAt = 30f;

		public const float BarResponse = 14f;
	}

	public static class Display
	{
		public const int InitialWidth = 1280;

		public const int InitialHeight = 800;

		public const int TargetFps = 144;

		public const float VignetteOpacity = 0.17f;
	}

	public static class Portal
	{
		public static readonly Vector3 First = new Vector3(0f, 0f, 3.5f);

		public static readonly Vector3 FirstNormal = Vector3.UnitY;

		public static readonly Vector3 Second = new Vector3(7f, 2.45f, -5f);

		public static readonly Vector3 SecondNormal = Vector3.Normalize(new Vector3(-0.36f, 0f, 0.93f));

		public const float Radius = 2.36f;

		public const float FrameBorder = 0.34f;

		public const float FrameHalfThickness = 0.12f;

		public const float EntryDepth = 0.18f;

		public const float TunnelDepth = 6f;

		public const float TransitTime = 1.55f;

		public const float ExitUpSpeed = 6.2f;

		public const float ExitForwardSpeed = 5f;

		public const int Layers = 10;

		public const float Depth = 0.38f;

		public const float Fade = 1.5f;

		public const float AnimationSpeed = 0.1f;
	}

	public static class Outline
	{
		public static readonly Vector4 Color = new Vector4(0.015f, 0.02f, 0.025f, 0.78f);

		public static readonly Vector4 ThicknessAndDistance = new Vector4(0.5f, 1.5f, 2f, 75f);

		public static readonly Vector4 EdgeThresholds = new Vector4(0.025f, 0.5f, 0.2f, 45f);
	}

	public static class Sky
	{
		public static readonly Vector3 DaySun = Vector3.Normalize(new Vector3(-0.38f, 0.43f, -0.79f));

		public static readonly Vector3 SunsetSun = Vector3.Normalize(new Vector3(-0.51f, 0.085f, -0.86f));

		public static readonly Vector3 NightSun = Vector3.Normalize(new Vector3(-0.57f, -0.63f, 0.52f));

		public static readonly Vector3 Moon = Vector3.Normalize(new Vector3(0.57f, 0.39f, -0.72f));

		public static readonly Vector3 DayTop = new Vector3(0.1f, 0.6f, 1f);

		public static readonly Vector3 DayHorizon = new Vector3(0.4f, 0.8f, 1f);

		public static readonly Vector3 SunsetTop = new Vector3(0.48f, 0.48f, 0.88f);

		public static readonly Vector3 SunsetHorizon = new Vector3(1f, 0.52f, 0.65f);

		public static readonly Vector3 NightTop = new Vector3(0.035f, 0.026f, 0.13f);

		public static readonly Vector3 NightHorizon = new Vector3(0.1f, 0.075f, 0.24f);

		public const float SunRadius = 0.075f;

		public const float MoonRadius = 0.06f;

		public const float CloudSpeed = 0.018f;

		public const float StarSpeed = 0.004f;

		public const float TransitionSpeed = 4.8f;
	}
}
