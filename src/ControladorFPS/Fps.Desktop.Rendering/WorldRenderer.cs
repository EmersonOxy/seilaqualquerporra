using System;
using System.Numerics;
using System.Collections.Generic;
using Fps.Desktop.Player;
using Fps.Desktop.Gameplay;
using Fps.Desktop.Scenes;
using Raylib_cs;

namespace Fps.Desktop.Rendering;

internal sealed class WorldRenderer : IDisposable
{
	public static readonly Color Sky = new Color(160, 170, 178, 255);

	private readonly Model floor;

	private readonly Model cube;

	private readonly Model orb;

	private readonly Model waterPlane;

    private readonly Model cylinder;

	private readonly Texture2D checker;

	private readonly Shader lighting;

	private readonly int viewPositionLocation;

	private readonly int lightDirectionLocation;

	private readonly int fogColorLocation;

	private readonly int nightAmountLocation;

	private readonly int surfaceModeLocation;

	private readonly int animationTimeLocation;

	private readonly int dissolveProgressLocation;

	private readonly int dissolveOriginLocation;

	private readonly int waveAmplitudeLocation;

	private readonly ParallaxPortalRenderer portal = new ParallaxPortalRenderer();

	private readonly PortalGeometry[] portalGeometries;
    private readonly ScenePortal[] portalEndpoints;

	private const string VertexShader = "#version 330\nin vec3 vertexPosition;\nin vec2 vertexTexCoord;\nin vec3 vertexNormal;\nuniform mat4 mvp;\nuniform mat4 matModel;\nuniform mat4 matNormal;\nuniform int surfaceMode;\nuniform float animationTime;\nuniform float waveAmplitude;\nout vec2 uv;\nout vec3 worldPosition;\nout vec3 normal;\n\nvoid main() {\n    uv = vertexTexCoord;\n    vec3 vertex = vertexPosition;\n    vec3 initialWorld = (matModel * vec4(vertexPosition, 1.0)).xyz;\n    if (surfaceMode == 3) {\n        vertex.y += sin(initialWorld.x * 1.2 + animationTime) * waveAmplitude * 0.7 +\n            cos(initialWorld.z * 1.35 - animationTime * 0.8) * waveAmplitude * 0.45;\n    }\n    worldPosition = (matModel * vec4(vertex, 1.0)).xyz;\n    normal = (matNormal * vec4(vertexNormal, 0.0)).xyz;\n    gl_Position = mvp * vec4(vertex, 1.0);\n}";

	private const string FragmentShader = @"#version 330
in vec2 uv;
in vec3 worldPosition;
in vec3 normal;
uniform sampler2D texture0;
uniform vec4 colDiffuse;
uniform vec3 viewPosition;
uniform vec3 lightDirection;
uniform vec3 fogColor;
uniform float nightAmount;
uniform int surfaceMode;
uniform float animationTime;
uniform float dissolveProgress;
uniform vec2 dissolveOriginUv;
uniform vec3 hole0;
uniform vec3 hole1;
uniform vec3 hole2;
uniform vec3 hole3;
uniform vec3 hole4;
uniform vec3 hole5;
out vec4 finalColor;

bool insideHole(vec3 h) {
    return h.z > 0.0 && worldPosition.y < 0.001 &&
        distance(worldPosition.xz, h.xy) < h.z;
}
float hash3(vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}
vec3 hsvToRgb(vec3 c) {
    vec3 rgb = clamp(abs(mod(c.x * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return c.z * mix(vec3(1.0), rgb, c.y);
}
void main() {
    if (insideHole(hole0) || insideHole(hole1) || insideHole(hole2) ||
        insideHole(hole3) || insideHole(hole4) || insideHole(hole5)) discard;
    vec4 base = texture(texture0, uv) * colDiffuse;
    float light = mix(0.72, 0.48, nightAmount) +
        mix(0.28, 0.22, nightAmount) * max(dot(normalize(normal), lightDirection), 0.0);
    float distanceFactor = length(viewPosition - worldPosition) * 0.023;
    float fog = 1.0 - exp(-distanceFactor * distanceFactor);
    vec3 shaded = mix(base.rgb * light, fogColor, fog);
    vec3 viewDir = normalize(viewPosition - worldPosition);
    float fresnel = pow(1.0 - max(dot(normalize(normal), viewDir), 0.0), 4.0);
    float alpha = base.a;
    if (surfaceMode == 1) {
        // Transparent glass with a bright Fresnel edge and restrained blue tint.
        float specular = pow(max(dot(reflect(-normalize(lightDirection), normalize(normal)), viewDir), 0.0), 36.0);
        shaded = mix(shaded, vec3(0.43, 0.78, 0.9), 0.28 + fresnel * 0.48);
        shaded += vec3(0.45, 0.75, 0.9) * fresnel * 0.35 + vec3(specular * 0.8);
        alpha = 0.3 + fresnel * 0.42;
    } else if (surfaceMode == 2) {
        float hue = fract(fresnel * 0.72 + worldPosition.y * 0.08 + animationTime * 0.11);
        vec3 rainbow = hsvToRgb(vec3(hue, 0.82, 1.0));
        float scan = 0.88 + 0.12 * sin(worldPosition.y * 48.0 - animationTime * 4.0);
        shaded = mix(shaded * 0.24, rainbow, 0.8) * scan + rainbow * 0.24;
        alpha = 0.94;
    } else if (surfaceMode == 3) {
        vec2 p = worldPosition.xz;
        float waveA = sin(p.x * 1.55 + animationTime * 1.2 + sin(p.y * 0.8 + animationTime));
        float waveB = sin(p.y * 1.9 - animationTime * 0.82 + sin(p.x * 1.25 + animationTime * 0.7));
        float pattern = waveA * 0.5 + waveB * 0.5;
        float foam = smoothstep(0.72, 0.94, pattern + 0.18 * sin(p.x * 3.8 + p.y * 2.7 + animationTime));
        vec3 water = mix(vec3(0.025, 0.24, 0.55), vec3(0.08, 0.62, 0.86), pattern * 0.5 + 0.5);
        water = mix(water, vec3(0.72, 0.94, 0.96), foam * 0.8);
        water += fresnel * vec3(0.16, 0.3, 0.4);
        shaded = water;
        alpha = 0.88;
    } else if (surfaceMode == 4) {
        float noise = hash3(floor(worldPosition * 13.0));
        vec2 uvDelta = abs(uv - dissolveOriginUv);
        uvDelta.x = min(uvDelta.x, 1.0 - uvDelta.x);
        float distanceFromStart = length(uvDelta) + noise * 0.12;
        float edgeRadius = dissolveProgress * 0.82;
        if (distanceFromStart < edgeRadius) discard;
        float burn = 1.0 - smoothstep(0.0, 0.075, abs(distanceFromStart - edgeRadius));
        shaded = mix(shaded, vec3(1.0, 0.34, 0.035), burn);
        shaded += vec3(0.95, 0.16, 0.015) * burn * 0.45;
    }
    finalColor = vec4(shaded, alpha);
}";

	public unsafe WorldRenderer(SceneDefinition scene)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_015c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Unknown result type (might be due to invalid IL or missing references)
		//IL_0166: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_017c: Unknown result type (might be due to invalid IL or missing references)
		//IL_018e: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		int num = (int)MathF.Round(scene.BaseSize / scene.TileSize);
		Image val = Raylib.GenImageChecked(num * 32, num * 32, 32, 32, new Color(122, 122, 122, 255), new Color(81, 81, 81, 255));
		checker = Raylib.LoadTextureFromImage(val);
		Raylib.UnloadImage(val);
		Raylib.GenTextureMipmaps(ref checker);
		Raylib.SetTextureFilter(checker, (TextureFilter)4);
		lighting = Raylib.LoadShaderFromMemory(VertexShader, FragmentShader);
		viewPositionLocation = Raylib.GetShaderLocation(lighting, "viewPosition");
		lightDirectionLocation = Raylib.GetShaderLocation(lighting, "lightDirection");
		fogColorLocation = Raylib.GetShaderLocation(lighting, "fogColor");
		nightAmountLocation = Raylib.GetShaderLocation(lighting, "nightAmount");
		surfaceModeLocation = Raylib.GetShaderLocation(lighting, "surfaceMode");
		animationTimeLocation = Raylib.GetShaderLocation(lighting, "animationTime");
		dissolveProgressLocation = Raylib.GetShaderLocation(lighting, "dissolveProgress");
		dissolveOriginLocation = Raylib.GetShaderLocation(lighting, "dissolveOriginUv");
		waveAmplitudeLocation = Raylib.GetShaderLocation(lighting, "waveAmplitude");
        List<PortalGeometry> geometries = new();
        List<ScenePortal> endpoints = new();
        foreach (ScenePortalPair pair in scene.PortalPairs)
        {
            geometries.Add(new PortalGeometry(pair.First));
            geometries.Add(new PortalGeometry(pair.Second));
            endpoints.Add(pair.First);
            endpoints.Add(pair.Second);
        }
        portalGeometries = geometries.ToArray();
        portalEndpoints = endpoints.ToArray();
        for (int i = 0; i < 6; i++)
        {
            Vector3 hole = new Vector3(9999f, 9999f, 0f);
            if (i < portalEndpoints.Length && portalEndpoints[i].InFloor)
            {
                ScenePortal p = portalEndpoints[i];
                hole = new Vector3(p.Center.X, p.Center.Z, p.Radius);
            }
            Raylib.SetShaderValue<Vector3>(lighting, Raylib.GetShaderLocation(lighting, "hole" + i),
                hole, (ShaderUniformDataType)2);
        }
		floor = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(scene.BaseSize, scene.BaseSize, 1, 1));
		floor.Materials[0].Shader = lighting;
		Raylib.SetMaterialTexture(ref *floor.Materials, (MaterialMapIndex)0, checker);
		cube = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
		cube.Materials[0].Shader = lighting;
		orb = Raylib.LoadModelFromMesh(Raylib.GenMeshSphere(.24f, 16, 20));
		orb.Materials[0].Shader = lighting;
		waterPlane = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(1f, 1f, 1, 1));
		waterPlane.Materials[0].Shader = lighting;
        cylinder = Raylib.LoadModelFromMesh(Raylib.GenMeshCylinder(.5f, 1f, 18));
        cylinder.Materials[0].Shader = lighting;
	}


	public void Draw(SceneDefinition scene, CameraPose pose, PortalTravelView? travel, SkyLighting sky, CollectibleManager collectibles, float animationTime)
	{
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0122: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Unknown result type (might be due to invalid IL or missing references)
		Camera3D val = new Camera3D(pose.Eye, pose.Eye + pose.Forward, pose.Up, pose.VerticalFov, (CameraProjection)0);
		Raylib.SetShaderValue<Vector3>(lighting, viewPositionLocation, pose.Eye, (ShaderUniformDataType)2);
		Raylib.SetShaderValue<Vector3>(lighting, lightDirectionLocation, sky.Direction, (ShaderUniformDataType)2);
		Raylib.SetShaderValue<Vector3>(lighting, fogColorLocation, sky.Fog, (ShaderUniformDataType)2);
		Raylib.SetShaderValue<float>(lighting, nightAmountLocation, sky.Night, (ShaderUniformDataType)0);
		Raylib.SetShaderValue<float>(lighting, animationTimeLocation, animationTime, (ShaderUniformDataType)0);
		Raylib.BeginMode3D(val);
		SetSurfaceMode(0);
		Raylib.DrawModel(floor, Vector3.Zero, 1f, Color.White);
		ReadOnlySpan<SceneBox> boxes = scene.Boxes;
		for (int i = 0; i < boxes.Length; i++)
		{
			SceneBox sceneBox = boxes[i];
			SetSurfaceMode(MaterialMode(sceneBox.Surface));
			Color val2 = sceneBox.Surface switch
			{
				Surface.Steel => new Color(104, 112, 118, 255), 
				Surface.Edge => new Color(181, 149, 85, 255), 
				_ => new Color(76, 85, 92, 255), 
			};
            SceneTransform.ToAxisAngle(SceneTransform.Orientation(sceneBox.RotationDegrees),
                out Vector3 axis, out float degrees);
            Raylib.DrawModelEx(cube, sceneBox.Center, axis, degrees, sceneBox.Size, val2);
		}
		SetSurfaceMode(0);
		foreach (SceneWater water in scene.Water)
		{
			SetSurfaceMode(3);
			Raylib.SetShaderValue<float>(lighting, waveAmplitudeLocation,
				water.Size.Y, (ShaderUniformDataType)0);
			Raylib.DrawModelEx(waterPlane, water.Center, Vector3.UnitY, water.RotationDegrees.Y,
				new Vector3(water.Size.X, 1f, water.Size.Z), new Color(125, 210, 245, 255));
		}
		SetSurfaceMode(0);
		foreach (SceneGrass grass in scene.Grass) DrawGrass(grass, animationTime);
		for (int i = 0; i < collectibles.Items.Count; i++)
		{
			CollectibleItem item = collectibles.Items[i];
			if (item.Collected && item.DissolveProgress >= 1f)
                continue;
			if (item.Collected)
			{
				SetSurfaceMode(4);
				Raylib.SetShaderValue<float>(lighting, dissolveProgressLocation,
					item.DissolveProgress, (ShaderUniformDataType)0);
				Raylib.SetShaderValue<Vector2>(lighting, dissolveOriginLocation,
					item.DissolveOriginUv, (ShaderUniformDataType)1);
				Raylib.DrawModel(orb, item.Position, 1f, new Color(250, 224, 125, 255));
				SetSurfaceMode(0);
				continue;
			}
            bool active = collectibles.Available(item);
			Vector3 center = item.Position + Vector3.UnitY * (MathF.Sin(animationTime * 2.8f + i) * 0.09f);
			Raylib.DrawSphereEx(center, active ? 0.24f : .18f, 16, 20,
                active ? new Color(250, 224, 125, 255) : new Color(110, 118, 124, 225));
			Raylib.DrawSphereWires(center, 0.3f, 6, 8,
                active ? new Color(255, 250, 215, 220) : new Color(160, 171, 185, 175));
		}
		foreach (CollectibleParticle particle in collectibles.Particles)
		{
			int alpha = (int)(particle.Opacity * 255f);
			Raylib.DrawSphere(particle.Position, particle.Size,
				new Color(255, (byte)(125 + particle.Opacity * 105f), 30, (byte)alpha));
		}
        foreach (SceneRamp ramp in scene.Ramps)
        {
            SetSurfaceMode(MaterialMode(ramp.Surface));
            Raylib.DrawModelEx(cube, ramp.Center, ramp.RotationAxis, ramp.AngleDegrees,
                ramp.BoxSize, new Color(187, 155, 100, 255));
        }
        SetSurfaceMode(0);
        foreach (SceneCylinder cyl in scene.Cylinders)
        {
            SetSurfaceMode(MaterialMode(cyl.Surface));
            Quaternion orientation = SceneTransform.Orientation(cyl.RotationDegrees);
            SceneTransform.ToAxisAngle(orientation, out Vector3 axis, out float degrees);
            Vector3 basePos = cyl.Center -
                Vector3.Transform(Vector3.UnitY * cyl.Size.Y * .5f, orientation);
            Raylib.DrawModelEx(cylinder, basePos, axis, degrees, cyl.Size,
                new Color(96, 156, 171, 255));
        }
        SetSurfaceMode(0);
        foreach (SceneLadder ladder in scene.Ladders)
        {
            Quaternion orientation = SceneTransform.Orientation(ladder.RotationDegrees);
            Vector3 left = Vector3.Transform(new Vector3(-ladder.Size.X * .5f, 0f, 0f), orientation);
            Vector3 right = Vector3.Transform(new Vector3(ladder.Size.X * .5f, 0f, 0f), orientation);
            float halfHeight = ladder.Size.Y * .5f;
            Raylib.DrawLine3D(ladder.Center + left - Vector3.UnitY * halfHeight,
                ladder.Center + left + Vector3.UnitY * halfHeight, new Color(210, 184, 85, 255));
            Raylib.DrawLine3D(ladder.Center + right - Vector3.UnitY * halfHeight,
                ladder.Center + right + Vector3.UnitY * halfHeight, new Color(210, 184, 85, 255));
            for (float y = -halfHeight + .28f; y < halfHeight; y += .32f)
                Raylib.DrawLine3D(ladder.Center + left + Vector3.UnitY * y,
                    ladder.Center + right + Vector3.UnitY * y,
                    new Color(228, 214, 132, 255));
        }
        for (int i = 0; i < portalEndpoints.Length; i++)
        {
            ScenePortal opening = portalEndpoints[i];
            if (Vector3.DistanceSquared(opening.Center, pose.Eye) > 85f * 85f)
                continue;
            portalGeometries[i].Draw(travel);
            portal.Draw(opening, pose.Eye, travel?.Opening == opening, sky.Fog);
        }
		Raylib.EndMode3D();
	}

	private void SetSurfaceMode(int mode) =>
		Raylib.SetShaderValue<int>(lighting, surfaceModeLocation, mode, (ShaderUniformDataType)4);

	private static int MaterialMode(Surface surface) => surface switch
	{
		Surface.Glass => 1,
		Surface.Holographic => 2,
		_ => 0
	};

	private static void DrawGrass(SceneGrass grass, float time)
	{
		int count = Math.Clamp((int)(grass.Size.X * grass.Size.Z * 3.2f), 12, 240);
		uint seed = unchecked((uint)(count * 747796405 + (int)(grass.Center.X * 1777f) + (int)(grass.Center.Z * 2153f)));
		Quaternion rotation = SceneTransform.Orientation(grass.RotationDegrees);
		for (int i = 0; i < count; i++)
		{
			float rx = NextGrassRandom(ref seed) - .5f;
			float rz = NextGrassRandom(ref seed) - .5f;
			float h = .28f + NextGrassRandom(ref seed) * .52f;
			Vector3 local = new(rx * grass.Size.X, 0f, rz * grass.Size.Z);
			Vector3 start = grass.Center + Vector3.Transform(local, rotation);
			float phase = time * (1.1f + NextGrassRandom(ref seed) * .7f) + start.X * .7f + start.Z * .43f;
			Vector3 sway = Vector3.Transform(new Vector3(MathF.Sin(phase) * .1f, 0f,
				MathF.Cos(phase * .8f) * .07f), rotation);
			Vector3 tip = start + Vector3.UnitY * h + sway;
			byte green = (byte)(115 + NextGrassRandom(ref seed) * 95f);
			Raylib.DrawCylinderEx(start, tip, .035f, .0015f, 4,
				new Color(42, green, 48, 255));
		}
	}

	private static float NextGrassRandom(ref uint state)
	{
		state ^= state << 13;
		state ^= state >> 17;
		state ^= state << 5;
		return (state & 0x00FFFFFF) / 16777216f;
	}

	public void Dispose()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		portal.Dispose();
		Raylib.UnloadModel(cube);
		Raylib.UnloadModel(orb);
		Raylib.UnloadModel(waterPlane);
        Raylib.UnloadModel(cylinder);
		Raylib.UnloadModel(floor);
		Raylib.UnloadTexture(checker);
		Raylib.UnloadShader(lighting);
	}

	static WorldRenderer()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
	}
}
