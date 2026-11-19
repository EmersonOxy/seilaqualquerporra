using System.Numerics;

namespace Fps.Desktop.Input;

internal readonly record struct InputState(Vector2 Movement, Vector2 Mouse, bool Sprint, bool Slow, bool CrouchHeld, bool ToggleCrouch, bool Jump = false);
