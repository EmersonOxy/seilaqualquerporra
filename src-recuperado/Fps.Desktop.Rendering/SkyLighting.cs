using System.Numerics;

namespace Fps.Desktop.Rendering;

internal readonly record struct SkyLighting(Vector3 Sun, Vector3 Direction, Vector3 Zenith, Vector3 Horizon, Vector3 Fog, float Sunset, float Night);
