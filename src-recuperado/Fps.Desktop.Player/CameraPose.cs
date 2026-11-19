using System.Numerics;

namespace Fps.Desktop.Player;

internal readonly record struct CameraPose(Vector3 Eye, Vector3 Forward, Vector3 Up, float VerticalFov);
