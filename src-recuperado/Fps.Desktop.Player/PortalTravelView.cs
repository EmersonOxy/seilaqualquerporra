using System.Numerics;
using Fps.Desktop.Scenes;

namespace Fps.Desktop.Player;

internal readonly record struct PortalTravelView(Vector3 Eye, Vector3 Direction, ScenePortal Opening, Vector3 Side, float Darkness);
