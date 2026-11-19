using System;
using System.Numerics;
using Fps.Desktop.Input;
using Fps.Desktop.Physics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Player;

internal sealed class PlayerController(Vector2 spawn)
{
	private bool crouchToggle;

	private Vector2 velocity;

	private float verticalVelocity;

	private ScenePortal? exiting;

	private PortalTraversal? traversal;

	public Vector2 Feet { get; private set; } = spawn;

	public float Elevation { get; private set; }

	public bool Grounded { get; private set; } = true;

	public float Height { get; private set; } = 1.7f;

	public float EyeHeight => float.Lerp(1f, 1.6f, (Height - 1.1f) / 0.6f);

	public PortalTravelView? PortalView => traversal?.View;

	public bool IsTraversing => traversal != null;

	public float Speed { get; private set; }

	public bool Running { get; private set; }

	public StaminaMeter Stamina { get; } = new StaminaMeter();

	public void Stop()
	{
		ScenePortal? scenePortal = exiting;
		if (!scenePortal.HasValue)
		{
			velocity = Vector2.Zero;
		}
		Speed = 0f;
		Running = false;
	}

	public void Update(InputState input, CollisionWorld world, float yaw, float elapsed)
	{
		if (elapsed <= 0f)
		{
			return;
		}
		PortalTraversal portalTraversal = traversal;
		if (portalTraversal != null)
		{
			if (portalTraversal.Advance(elapsed))
			{
				FinishTraversal(portalTraversal);
			}
			return;
		}
		if (input.ToggleCrouch)
		{
			crouchToggle = !crouchToggle;
		}
		if (input.Jump && Grounded)
		{
			verticalVelocity = 4.8f;
			Grounded = false;
		}
		int num = (int)MathF.Ceiling(elapsed / (1f / 120f));
		float dt = elapsed / (float)num;
		(float Sin, float Cos) tuple = MathF.SinCos(yaw);
		float item = tuple.Sin;
		float item2 = tuple.Cos;
		for (int i = 0; i < num; i++)
		{
			if (traversal != null)
			{
				break;
			}
			Step(input, world, item, item2, dt);
		}
	}

	private void Step(InputState input, CollisionWorld world, float sin, float cos, float dt)
	{
		Vector2 movement = input.Movement;
		float num = movement.Length();
		bool flag = num > 0f && input.Sprint && !input.Slow && !Stamina.Exhausted;
		bool flag2 = crouchToggle || input.CrouchHeld;
		bool flag3 = Height == 1.7f || ((flag || !flag2) && world.Fits(Feet, 1.7f, 0.32f, Elevation));
		bool flag4 = flag & flag3;
		if (flag4)
		{
			crouchToggle = false;
		}
		float target = ((flag3 && (flag4 || !flag2)) ? 1.7f : 1.1f);
		Height = MathEx.Approach(Height, target, 3.5f * dt);
		float amount = (Height - 1.1f) / 0.6f;
		bool flag5 = flag4 && Height == 1.7f;
		float num2 = (flag5 ? 6.3f : float.Lerp(2.1f, 3.4f, amount));
		if (input.Slow)
		{
			num2 = MathF.Min(num2, 1.45f);
		}
		float num3 = ((num > 0f) ? (num2 / num) : 0f);
		Vector2 vector = new Vector2(movement.X * cos - movement.Y * sin, (0f - movement.X) * sin - movement.Y * cos) * num3;
		ScenePortal? scenePortal = exiting;
		if (!scenePortal.HasValue)
		{
			Vector2 vector2 = vector - velocity;
			float num4 = vector2.Length();
			bool num5 = num == 0f || Vector2.Dot(velocity, vector2) < 0f;
			float num6 = (Grounded ? 1f : 0.35f);
			float num7 = (num5 ? 34f : 27f) * num6 * dt;
			velocity = ((num4 <= num7) ? vector : (velocity + vector2 * (num7 / num4)));
		}
		Vector2 feet = Feet;
		float elevation = Elevation;
		Feet = world.Move(Feet, velocity * dt, Height, 0.32f, Elevation);
		Vector2 vector3 = (Feet - feet) / dt;
		if (Feet.X != feet.X + velocity.X * dt)
		{
			velocity.X = vector3.X;
		}
		if (Feet.Y != feet.Y + velocity.Y * dt)
		{
			velocity.Y = vector3.Y;
		}
		Speed = vector3.Length();
		UpdateElevation(world, dt);
		CheckPortalEntry(world, feet, elevation, new Vector3(0f - sin, 0f, 0f - cos));
		Running = ((traversal == null && Grounded) & flag5) && Speed > 3.41f;
		Stamina.Update(Running, dt);
	}

	private void UpdateElevation(CollisionWorld world, float dt)
	{
		verticalVelocity -= 12f * dt;
		float num = Elevation + verticalVelocity * dt;
		Elevation = world.MoveVertical(Feet, Elevation, num, Height, 0.32f);
		Grounded = Elevation != num && verticalVelocity < 0f;
		if (Elevation != num)
		{
			verticalVelocity = 0f;
		}
	}

	private void CheckPortalEntry(CollisionWorld world, Vector2 previous, float previousElevation, Vector3 forward)
	{
		ScenePortal? scenePortal = exiting;
		if (scenePortal.HasValue)
		{
			if (!scenePortal.GetValueOrDefault().Contains(Feet, -0.32f) || Grounded)
			{
				exiting = null;
			}
			return;
		}
		ScenePortalPair? portals = world.Portals;
		if (!portals.HasValue)
		{
			return;
		}
		ScenePortalPair valueOrDefault = portals.GetValueOrDefault();
		if (Elevation <= -0.18f && verticalVelocity < 0f)
		{
			scenePortal = valueOrDefault.FloorOpeningAt(Feet, 0.3199f);
			if (scenePortal.HasValue)
			{
				ScenePortal valueOrDefault2 = scenePortal.GetValueOrDefault();
				BeginTraversal(valueOrDefault2, valueOrDefault2.Normal, valueOrDefault.Other(valueOrDefault2), forward);
				return;
			}
		}
		Vector3 before = new Vector3(previous.X, previousElevation, previous.Y);
		Vector3 after = new Vector3(Feet.X, Elevation, Feet.Y);
		if (CrossesFrame(valueOrDefault.First, before, after, out var side))
		{
			BeginTraversal(valueOrDefault.First, side, valueOrDefault.Second, forward);
		}
		else if (CrossesFrame(valueOrDefault.Second, before, after, out side))
		{
			BeginTraversal(valueOrDefault.Second, side, valueOrDefault.First, forward);
		}
	}

	private bool CrossesFrame(ScenePortal portal, Vector3 before, Vector3 after, out Vector3 side)
	{
		side = default;
		if (portal.InFloor)
		{
			return false;
		}
		Vector3 vector = Vector3.UnitY * (Height / 2f);
		float num = portal.SignedDistance(before + vector);
		float num2 = portal.SignedDistance(after + vector);
		if (num == num2 || num * num2 > 0f)
		{
			return false;
		}
		Vector3 vector2 = Vector3.Lerp(before, after, num / (num - num2));
		if (!portal.FitsOpening(vector2 + Vector3.UnitY * 0.32f, vector2 + Vector3.UnitY * (Height - 0.32f), 0.32f))
		{
			return false;
		}
		side = ((num > num2) ? 1f : (-1f)) * portal.Normal;
		return true;
	}

	private void BeginTraversal(ScenePortal entry, Vector3 side, ScenePortal destination, Vector3 forward)
	{
		Vector3 startEye = new Vector3(Feet.X, Elevation + EyeHeight, Feet.Y);
		Vector3 vector = (destination.InFloor ? (destination.Center + Vector3.UnitY * 0.1f) : (destination.Center - Vector3.UnitY * (Height / 2f) + destination.Normal * (0.56f + MathF.Abs(destination.Normal.Y) * (Height / 2f - 0.32f))));
		vector.Y = MathF.Max(0.1f, vector.Y);
		Vector3 endEye = vector + Vector3.UnitY * EyeHeight;
		traversal = new PortalTraversal(entry, side, startEye, destination, endEye, forward);
		Grounded = false;
		Speed = 0f;
	}

	private void FinishTraversal(PortalTraversal active)
	{
		Vector3 vector = active.EndEye - Vector3.UnitY * EyeHeight;
		Feet = new Vector2(vector.X, vector.Z);
		Elevation = vector.Y;
		Vector3 vector2 = (active.Exit.InFloor ? active.FloorExitDirection : active.Exit.Normal);
		velocity = new Vector2(vector2.X, vector2.Z) * 5f;
		verticalVelocity = (active.Exit.InFloor ? 6.2f : (vector2.Y * 5f));
		exiting = (active.Exit.InFloor ? new ScenePortal?(active.Exit) : ((ScenePortal?)null));
		traversal = null;
	}
}
