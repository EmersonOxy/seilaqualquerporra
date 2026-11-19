using System;
using System.Numerics;
using System.Runtime.CompilerServices;
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

	private readonly Texture2D checker;

	private readonly Shader lighting;

	private readonly int viewPositionLocation;

	private readonly int lightDirectionLocation;

	private readonly int fogColorLocation;

	private readonly int nightAmountLocation;

	private readonly ParallaxPortalRenderer portal = new ParallaxPortalRenderer();

	private readonly PortalGeometry? firstGeometry;

	private readonly PortalGeometry? secondGeometry;

	private const string VertexShader = "#version 330\nin vec3 vertexPosition;\nin vec2 vertexTexCoord;\nin vec3 vertexNormal;\nuniform mat4 mvp;\nuniform mat4 matModel;\nuniform mat4 matNormal;\nout vec2 uv;\nout vec3 worldPosition;\nout vec3 normal;\n\nvoid main() {\n    uv = vertexTexCoord;\n    worldPosition = (matModel * vec4(vertexPosition, 1.0)).xyz;\n    normal = (matNormal * vec4(vertexNormal, 0.0)).xyz;\n    gl_Position = mvp * vec4(vertexPosition, 1.0);\n}";

	private const string FragmentShader = "#version 330\nin vec2 uv;\nin vec3 worldPosition;\nin vec3 normal;\nuniform sampler2D texture0;\nuniform vec4 colDiffuse;\nuniform vec3 viewPosition;\nuniform vec3 lightDirection;\nuniform vec3 fogColor;\nuniform float nightAmount;\nuniform vec3 holeA;\nuniform vec3 holeB;\nout vec4 finalColor;\n\nvoid main() {\n    if (worldPosition.y <= 0.001 &&\n        (distance(worldPosition.xz, holeA.xy) < holeA.z ||\n         distance(worldPosition.xz, holeB.xy) < holeB.z)) discard;\n\n    vec4 base = texture(texture0, uv) * colDiffuse;\n    float light = mix(0.72, 0.48, nightAmount) +\n        mix(0.28, 0.22, nightAmount) * max(dot(normalize(normal), lightDirection), 0.0);\n    float distanceFactor = length(viewPosition - worldPosition) * 0.023;\n    float fog = 1.0 - exp(-distanceFactor * distanceFactor);\n    finalColor = vec4(mix(base.rgb * light, fogColor, fog), base.a);\n}";

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
		int num = (int)MathF.Round(scene.Size / scene.TileSize);
		Image val = Raylib.GenImageChecked(num * 32, num * 32, 32, 32, new Color(122, 122, 122, 255), new Color(81, 81, 81, 255));
		checker = Raylib.LoadTextureFromImage(val);
		Raylib.UnloadImage(val);
		Raylib.GenTextureMipmaps(ref checker);
		Raylib.SetTextureFilter(checker, (TextureFilter)4);
		lighting = Raylib.LoadShaderFromMemory("#version 330\nin vec3 vertexPosition;\nin vec2 vertexTexCoord;\nin vec3 vertexNormal;\nuniform mat4 mvp;\nuniform mat4 matModel;\nuniform mat4 matNormal;\nout vec2 uv;\nout vec3 worldPosition;\nout vec3 normal;\n\nvoid main() {\n    uv = vertexTexCoord;\n    worldPosition = (matModel * vec4(vertexPosition, 1.0)).xyz;\n    normal = (matNormal * vec4(vertexNormal, 0.0)).xyz;\n    gl_Position = mvp * vec4(vertexPosition, 1.0);\n}", "#version 330\nin vec2 uv;\nin vec3 worldPosition;\nin vec3 normal;\nuniform sampler2D texture0;\nuniform vec4 colDiffuse;\nuniform vec3 viewPosition;\nuniform vec3 lightDirection;\nuniform vec3 fogColor;\nuniform float nightAmount;\nuniform vec3 holeA;\nuniform vec3 holeB;\nout vec4 finalColor;\n\nvoid main() {\n    if (worldPosition.y <= 0.001 &&\n        (distance(worldPosition.xz, holeA.xy) < holeA.z ||\n         distance(worldPosition.xz, holeB.xy) < holeB.z)) discard;\n\n    vec4 base = texture(texture0, uv) * colDiffuse;\n    float light = mix(0.72, 0.48, nightAmount) +\n        mix(0.28, 0.22, nightAmount) * max(dot(normalize(normal), lightDirection), 0.0);\n    float distanceFactor = length(viewPosition - worldPosition) * 0.023;\n    float fog = 1.0 - exp(-distanceFactor * distanceFactor);\n    finalColor = vec4(mix(base.rgb * light, fogColor, fog), base.a);\n}");
		viewPositionLocation = Raylib.GetShaderLocation(lighting, "viewPosition");
		lightDirectionLocation = Raylib.GetShaderLocation(lighting, "lightDirection");
		fogColorLocation = Raylib.GetShaderLocation(lighting, "fogColor");
		nightAmountLocation = Raylib.GetShaderLocation(lighting, "nightAmount");
		ScenePortalPair? portals = scene.Portals;
		if (portals.HasValue)
		{
			ScenePortalPair valueOrDefault = portals.GetValueOrDefault();
			SetFloorOpening("holeA", valueOrDefault.First);
			SetFloorOpening("holeB", valueOrDefault.Second);
			firstGeometry = new PortalGeometry(valueOrDefault.First);
			secondGeometry = new PortalGeometry(valueOrDefault.Second);
		}
		floor = Raylib.LoadModelFromMesh(Raylib.GenMeshPlane(scene.Size, scene.Size, 1, 1));
		floor.Materials[0].Shader = lighting;
		Raylib.SetMaterialTexture(ref *floor.Materials, (MaterialMapIndex)0, checker);
		cube = Raylib.LoadModelFromMesh(Raylib.GenMeshCube(1f, 1f, 1f));
		cube.Materials[0].Shader = lighting;
	}

	private void SetFloorOpening(string uniform, ScenePortal opening)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		Vector3 vector = (opening.InFloor ? new Vector3(opening.FloorCenter, opening.Radius) : Vector3.Zero);
		Raylib.SetShaderValue<Vector3>(lighting, Raylib.GetShaderLocation(lighting, uniform), vector, (ShaderUniformDataType)2);
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
		Raylib.BeginMode3D(val);
		Raylib.DrawModel(floor, Vector3.Zero, 1f, Color.White);
		ReadOnlySpan<SceneBox> boxes = scene.Boxes;
		for (int i = 0; i < boxes.Length; i++)
		{
			SceneBox sceneBox = boxes[i];
			Color val2 = sceneBox.Surface switch
			{
				Surface.Steel => new Color(104, 112, 118, 255), 
				Surface.Edge => new Color(181, 149, 85, 255), 
				_ => new Color(76, 85, 92, 255), 
			};
			Raylib.DrawModelEx(cube, sceneBox.Center, Vector3.UnitY, 0f, sceneBox.Size, val2);
		}
		for (int i = 0; i < collectibles.Items.Count; i++)
		{
			CollectibleItem item = collectibles.Items[i];
			if (item.Collected || !collectibles.Available(item))
				continue;
			Vector3 center = item.Position + Vector3.UnitY * (MathF.Sin(animationTime * 2.8f + i) * 0.09f);
			Raylib.DrawSphereEx(center, 0.24f, 6, 8, new Color(250, 224, 125, 255));
			Raylib.DrawSphereWires(center, 0.3f, 6, 8, new Color(255, 250, 215, 220));
		}
		ScenePortalPair? portals = scene.Portals;
		if (portals.HasValue)
		{
			ScenePortalPair valueOrDefault = portals.GetValueOrDefault();
			firstGeometry?.Draw(travel);
			secondGeometry?.Draw(travel);
			portal.Draw(valueOrDefault.First, pose.Eye, travel?.Opening == valueOrDefault.First, sky.Fog);
			portal.Draw(valueOrDefault.Second, pose.Eye, travel?.Opening == valueOrDefault.Second, sky.Fog);
		}
		Raylib.EndMode3D();
	}

	public void Dispose()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		portal.Dispose();
		Raylib.UnloadModel(cube);
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
