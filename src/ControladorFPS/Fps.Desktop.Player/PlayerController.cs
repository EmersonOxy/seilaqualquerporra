using System;
using System.Numerics;
using Fps.Desktop.Input;
using Fps.Desktop.Physics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Player;

internal sealed class PlayerController(Vector2 spawn)
{
	private const float StandingHeight = 1.60f;
	private const float CrouchedHeight = 1.10f;
	private const float HighFallVoiceHeight = 3.5f;
	private const float HighFallVoiceDelay = 0.65f;

	private bool crouchToggle;

	private bool highFallVoicePlayed;

	private float highFallVoiceDelayElapsed;

	private Vector2 velocity;

    private Vector2 slopeVelocity;

	private float verticalVelocity;

	private ScenePortal? exiting;

	private PortalTraversal? traversal;

	public Vector2 Feet { get; private set; } = spawn;

	public float Elevation { get; private set; }

	public bool Grounded { get; private set; } = true;

	public float Height { get; private set; } = StandingHeight;

	public float EyeHeight => float.Lerp(1f, 1.5f, (Height - CrouchedHeight) / (StandingHeight - CrouchedHeight));

	public PortalTravelView? PortalView => traversal?.View;

	public bool IsTraversing => traversal != null;

	public float Speed { get; private set; }

	public bool Running { get; private set; }

	public StaminaMeter Stamina { get; } = new StaminaMeter();

	public event Action<ScenePortal, ScenePortal>? PortalTraversed;

	public event Action? FellFromHighPlace;

    public void Respawn(Vector2 spawn)
    {
        Feet = spawn;
        Elevation = 0f;
        Grounded = true;
        Height = StandingHeight;
        crouchToggle = false;
        velocity = Vector2.Zero;
        verticalVelocity = 0f;
        highFallVoicePlayed = false;
		highFallVoiceDelayElapsed = 0f;
        exiting = null;
        traversal = null;
        Stop();
    }

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
        // A vertical ladder is climbed with W and descended with S.
        // Jump still exits the ladder; gravity is disabled only while climbing.
        if (MathF.Abs(movement.Y) > .01f && !input.Jump &&
            world.TryGetLadder(Feet, Elevation, Height, out SceneLadder ladder))
        {
            float bottom = ladder.Center.Y - ladder.Size.Y * .5f;
            float top = ladder.Center.Y + ladder.Size.Y * .5f + .18f;
            float desired = Math.Clamp(Elevation + movement.Y * 2.35f * dt,
                bottom - .12f, top);
            if (world.Fits(Feet, Height, .32f, desired))
                Elevation = desired;
            verticalVelocity = 0f;
            velocity = Vector2.Zero;
            Running = false;
            Speed = 0f;
            Grounded = false;
            Stamina.Update(false, dt);
            if (movement.Y > 0f && Elevation >= top - .03f)
            {
                // Step inward from the ladder onto the upper landing.
                Vector3 forward = Vector3.Transform(Vector3.UnitZ,
                    SceneTransform.Orientation(ladder.RotationDegrees));
                Vector2 delta = new(-forward.X * 2.4f * dt, -forward.Z * 2.4f * dt);
                float footHeight = Elevation;
                Feet = world.Move(Feet, delta, Height, .32f, ref footHeight, false);
                Elevation = footHeight;
            }
            return;
        }
		bool flag = num > 0f && input.Sprint && !input.Slow && !Stamina.Exhausted;
		bool flag2 = crouchToggle || input.CrouchHeld;
		bool flag3 = Height == StandingHeight || ((flag || !flag2) && world.Fits(Feet, StandingHeight, 0.32f, Elevation));
		bool flag4 = flag & flag3;
		if (flag4)
		{
			crouchToggle = false;
		}
		float target = ((flag3 && (flag4 || !flag2)) ? StandingHeight : CrouchedHeight);
		Height = MathEx.Approach(Height, target, 3.5f * dt);
		float amount = (Height - CrouchedHeight) / (StandingHeight - CrouchedHeight);
		bool flag5 = flag4 && Height == StandingHeight;
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
        // Independent gravity-induced sliding means neutral movement no longer
        // freezes the capsule on a steep incline. It works even when WASD is idle.
        if (Grounded && world.TryRampSlide(Feet, Elevation, out Vector2 downhill, out float gravityFactor))
        {
            float acceleration = 12f * gravityFactor;
            slopeVelocity += downhill * (acceleration * dt);
            if (slopeVelocity.LengthSquared() > 6f * 6f)
                slopeVelocity = Vector2.Normalize(slopeVelocity) * 6f;
        }
        else
        {
            float damping = Math.Clamp(11f * dt, 0f, 1f);
            slopeVelocity *= 1f - damping;
        }

        Vector2 travelVelocity = velocity + slopeVelocity;
		Vector2 feet = Feet;
		float elevation = Elevation;
		float movedElevation = Elevation;
		Feet = world.Move(Feet, travelVelocity * dt, Height, 0.32f, ref movedElevation, Grounded);
		Elevation = movedElevation;
		Vector2 vector3 = (Feet - feet) / dt;
        if (MathF.Abs(Feet.X - (feet.X + travelVelocity.X * dt)) > .001f)
        {
            velocity.X = vector3.X;
            slopeVelocity.X = 0f;
        }
        if (MathF.Abs(Feet.Y - (feet.Y + travelVelocity.Y * dt)) > .001f)
        {
            velocity.Y = vector3.Y;
            slopeVelocity.Y = 0f;
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
        if (!Grounded && verticalVelocity <= 0f &&
            world.GroundBelow(Feet, Elevation, .32f, out float surfaceY) &&
            MathF.Abs(surfaceY - Elevation) <= .085f)
        {
            Elevation = surfaceY;
            Grounded = true;
        }
		if (Grounded || Elevation != num)
		{
			verticalVelocity = 0f;
		}
		if (Grounded || verticalVelocity >= 0f)
		{
			highFallVoicePlayed = false;
			highFallVoiceDelayElapsed = 0f;
		}
		else if (!highFallVoicePlayed &&
			world.TryGetNearestSupportBelow(Feet, Elevation, 0.32f, out float support) &&
			Elevation - support >= HighFallVoiceHeight)
		{
			highFallVoiceDelayElapsed += dt;
			if (highFallVoiceDelayElapsed >= HighFallVoiceDelay)
			{
				highFallVoicePlayed = true;
				FellFromHighPlace?.Invoke();
			}
		}
		else if (!highFallVoicePlayed)
		{
			highFallVoiceDelayElapsed = 0f;
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
        ReadOnlySpan<ScenePortalPair> pairs = world.PortalPairs;
        if (Elevation <= -.18f && verticalVelocity < 0f)
        {
            foreach (ScenePortalPair pair in pairs)
            {
                ScenePortal? hole = pair.OneWay ?
                    (pair.First.InFloor && pair.First.Contains(Feet, .3199f) ? pair.First : null) :
                    pair.FloorOpeningAt(Feet, .3199f);
                if (hole.HasValue)
                {
                    ScenePortal entry = hole.Value;
                    BeginTraversal(entry, entry.Normal, pair.Other(entry), forward);
                    return;
                }
            }
        }

        Vector3 before = new(previous.X, previousElevation, previous.Y);
        Vector3 after = new(Feet.X, Elevation, Feet.Y);
        foreach (ScenePortalPair pair in pairs)
        {
            if (CrossesFrame(pair.First, before, after, out Vector3 side))
            {
                BeginTraversal(pair.First, side, pair.Second, forward);
                return;
            }
            if (!pair.OneWay && CrossesFrame(pair.Second, before, after, out side))
            {
                BeginTraversal(pair.Second, side, pair.First, forward);
                return;
            }
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
		PortalTraversed?.Invoke(active.Entry, active.Exit);
	}
}
