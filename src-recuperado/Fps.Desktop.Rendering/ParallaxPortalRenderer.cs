using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Fps.Desktop.Scenes;
using Raylib_cs;

namespace Fps.Desktop.Rendering;

internal sealed class ParallaxPortalRenderer : IDisposable
{
	private readonly Texture2D noise;

	private readonly Shader shader;

	private readonly Model surface;

	private readonly int viewPositionLocation;

	private readonly int timeLocation;

	private readonly int openInteriorLocation;

	private readonly int fogColorLocation;

	private const string VertexShader = "#version 330\nin vec3 vertexPosition;\nin vec2 vertexTexCoord;\nin vec3 vertexNormal;\nin vec4 vertexTangent;\nuniform mat4 mvp;\nuniform mat4 matModel;\nuniform mat4 matNormal;\nout vec2 uv;\nout vec3 worldPosition;\nout vec3 normal;\nout vec3 tangent;\n\nvoid main() {\n    uv = vertexTexCoord;\n    worldPosition = (matModel * vec4(vertexPosition, 1.0)).xyz;\n    normal = mat3(matNormal) * vertexNormal;\n    tangent = mat3(matModel) * vertexTangent.xyz;\n    gl_Position = mvp * vec4(vertexPosition, 1.0);\n}";

	private const string FragmentShader = "#version 330\nin vec2 uv;\nin vec3 worldPosition;\nin vec3 normal;\nin vec3 tangent;\nuniform sampler2D texture0;\nuniform vec3 viewPosition;\nuniform vec3 fogColor;\nuniform float timeOffset;\nuniform int layers;\nuniform int openInterior;\nuniform float depthAmount;\nuniform float fadeAmount;\nout vec4 finalColor;\n\nvec2 toPolar(vec2 coord) {\n    vec2 p = coord * 2.0 - 1.0;\n    float angle = atan(p.x, p.y);\n    return vec2(angle / 3.14159265, length(p)) * 0.5;\n}\n\nfloat overlay(float a, float b) {\n    return mix(2.0 * a * b, 1.0 - 2.0 * (1.0 - a) * (1.0 - b), step(0.5, a));\n}\n\nvoid main() {\n    float aperture = length(uv * 2.0 - 1.0);\n    if (aperture > 1.0 || (openInterior == 1 && aperture < 0.93)) discard;\n\n    if (openInterior == 1) {\n        finalColor = vec4(0.38, 0.48, 0.53, 1.0);\n        return;\n    }\n\n    vec3 n = normalize(normal);\n    vec3 t = normalize(tangent - n * dot(n, tangent));\n    vec3 b = cross(n, t);\n    vec3 view = normalize(viewPosition - worldPosition);\n    vec3 viewTangent = vec3(dot(view, t), dot(view, b), dot(view, n));\n    vec2 offset = vec2(-viewTangent.x, viewTangent.y) * depthAmount / max(viewTangent.z, 0.06);\n\n    float value = 0.0;\n    for (int i = 0; i < layers; i++) {\n        float layer = float(i) / float(layers);\n        vec2 coord = uv + offset * layer;\n        float circle = clamp((length(coord * 2.0 - 1.0) - 0.2) / 0.8, 0.0, 1.0);\n        vec2 noiseUV = toPolar(coord) + vec2(layer, layer + timeOffset);\n        // O gradiente angular segue a volta completa sem saltar entre níveis de mipmap.\n        vec2 dx = dFdx(noiseUV);\n        vec2 dy = dFdy(noiseUV);\n        dx.x -= round(dx.x);\n        dy.x -= round(dy.x);\n        float noise = textureGrad(texture0, noiseUV, dx, dy).r;\n        float layerValue = step(0.5, overlay(circle, noise));\n        value = max(value, layerValue * pow(1.0 - layer, fadeAmount));\n    }\n\n    vec3 color = mix(vec3(value), vec3(0.35, 0.42, 0.46), smoothstep(0.945, 0.985, aperture));\n    float distanceFactor = length(viewPosition - worldPosition) * 0.023;\n    float fog = 1.0 - exp(-distanceFactor * distanceFactor);\n    finalColor = vec4(mix(color, fogColor, fog), 1.0);\n}";

	public unsafe ParallaxPortalRenderer()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		noise = CreateNoiseTexture();
		shader = Raylib.LoadShaderFromMemory("#version 330\nin vec3 vertexPosition;\nin vec2 vertexTexCoord;\nin vec3 vertexNormal;\nin vec4 vertexTangent;\nuniform mat4 mvp;\nuniform mat4 matModel;\nuniform mat4 matNormal;\nout vec2 uv;\nout vec3 worldPosition;\nout vec3 normal;\nout vec3 tangent;\n\nvoid main() {\n    uv = vertexTexCoord;\n    worldPosition = (matModel * vec4(vertexPosition, 1.0)).xyz;\n    normal = mat3(matNormal) * vertexNormal;\n    tangent = mat3(matModel) * vertexTangent.xyz;\n    gl_Position = mvp * vec4(vertexPosition, 1.0);\n}", "#version 330\nin vec2 uv;\nin vec3 worldPosition;\nin vec3 normal;\nin vec3 tangent;\nuniform sampler2D texture0;\nuniform vec3 viewPosition;\nuniform vec3 fogColor;\nuniform float timeOffset;\nuniform int layers;\nuniform int openInterior;\nuniform float depthAmount;\nuniform float fadeAmount;\nout vec4 finalColor;\n\nvec2 toPolar(vec2 coord) {\n    vec2 p = coord * 2.0 - 1.0;\n    float angle = atan(p.x, p.y);\n    return vec2(angle / 3.14159265, length(p)) * 0.5;\n}\n\nfloat overlay(float a, float b) {\n    return mix(2.0 * a * b, 1.0 - 2.0 * (1.0 - a) * (1.0 - b), step(0.5, a));\n}\n\nvoid main() {\n    float aperture = length(uv * 2.0 - 1.0);\n    if (aperture > 1.0 || (openInterior == 1 && aperture < 0.93)) discard;\n\n    if (openInterior == 1) {\n        finalColor = vec4(0.38, 0.48, 0.53, 1.0);\n        return;\n    }\n\n    vec3 n = normalize(normal);\n    vec3 t = normalize(tangent - n * dot(n, tangent));\n    vec3 b = cross(n, t);\n    vec3 view = normalize(viewPosition - worldPosition);\n    vec3 viewTangent = vec3(dot(view, t), dot(view, b), dot(view, n));\n    vec2 offset = vec2(-viewTangent.x, viewTangent.y) * depthAmount / max(viewTangent.z, 0.06);\n\n    float value = 0.0;\n    for (int i = 0; i < layers; i++) {\n        float layer = float(i) / float(layers);\n        vec2 coord = uv + offset * layer;\n        float circle = clamp((length(coord * 2.0 - 1.0) - 0.2) / 0.8, 0.0, 1.0);\n        vec2 noiseUV = toPolar(coord) + vec2(layer, layer + timeOffset);\n        // O gradiente angular segue a volta completa sem saltar entre níveis de mipmap.\n        vec2 dx = dFdx(noiseUV);\n        vec2 dy = dFdy(noiseUV);\n        dx.x -= round(dx.x);\n        dy.x -= round(dy.x);\n        float noise = textureGrad(texture0, noiseUV, dx, dy).r;\n        float layerValue = step(0.5, overlay(circle, noise));\n        value = max(value, layerValue * pow(1.0 - layer, fadeAmount));\n    }\n\n    vec3 color = mix(vec3(value), vec3(0.35, 0.42, 0.46), smoothstep(0.945, 0.985, aperture));\n    float distanceFactor = length(viewPosition - worldPosition) * 0.023;\n    float fog = 1.0 - exp(-distanceFactor * distanceFactor);\n    finalColor = vec4(mix(color, fogColor, fog), 1.0);\n}");
		viewPositionLocation = Raylib.GetShaderLocation(shader, "viewPosition");
		timeLocation = Raylib.GetShaderLocation(shader, "timeOffset");
		openInteriorLocation = Raylib.GetShaderLocation(shader, "openInterior");
		fogColorLocation = Raylib.GetShaderLocation(shader, "fogColor");
		Raylib.SetShaderValue<int>(shader, Raylib.GetShaderLocation(shader, "layers"), Math.Clamp(10, 1, 64), (ShaderUniformDataType)4);
		Raylib.SetShaderValue<float>(shader, Raylib.GetShaderLocation(shader, "depthAmount"), 0.38f, (ShaderUniformDataType)0);
		Raylib.SetShaderValue<float>(shader, Raylib.GetShaderLocation(shader, "fadeAmount"), 1.5f, (ShaderUniformDataType)0);
		Mesh val = Raylib.GenMeshPlane(1f, 1f, 1, 1);
		Raylib.GenMeshTangents(ref val);
		surface = Raylib.LoadModelFromMesh(val);
		surface.Materials[0].Shader = shader;
		Raylib.SetMaterialTexture(ref *surface.Materials, (MaterialMapIndex)0, noise);
	}

	public void Draw(ScenePortal portal, Vector3 eye, bool showInterior, Vector3 fogColor)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		Raylib.SetShaderValue<Vector3>(shader, viewPositionLocation, eye, (ShaderUniformDataType)2);
		Raylib.SetShaderValue<Vector3>(shader, fogColorLocation, fogColor, (ShaderUniformDataType)2);
		float num = (float)(Raylib.GetTime() * 0.10000000149011612 % 1.0);
		Raylib.SetShaderValue<float>(shader, timeLocation, num, (ShaderUniformDataType)0);
		Raylib.SetShaderValue<int>(shader, openInteriorLocation, showInterior ? 1 : 0, (ShaderUniformDataType)4);
		Vector3 vector = ((!portal.InFloor && portal.SignedDistance(eye) < 0f) ? (-portal.Normal) : portal.Normal);
		Vector3 value = Vector3.Cross(Vector3.UnitY, vector);
		value = ((value.LengthSquared() > 1E-06f) ? Vector3.Normalize(value) : Vector3.UnitX);
		float num2 = MathF.Acos(Math.Clamp(vector.Y, -1f, 1f)) * (180f / (float)Math.PI);
		float num3 = (portal.InFloor ? (-0.03f) : 0.122999996f);
		Vector3 vector2 = portal.Center + vector * num3;
		float num4 = (portal.Radius + 0.012f) * 2f;
		Raylib.DrawModelEx(surface, vector2, value, num2, new Vector3(num4, 1f, num4), Color.White);
	}

	public void Dispose()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		Raylib.UnloadModel(surface);
		Raylib.UnloadTexture(noise);
		Raylib.UnloadShader(shader);
	}

	private static Texture2D CreateNoiseTexture()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		Random random = new Random(420);
		float[] grid = CreateNoiseGrid(8, random);
		float[] grid2 = CreateNoiseGrid(24, random);
		Image val = Raylib.GenImageColor(256, 256, Color.Black);
		for (int i = 0; i < 256; i++)
		{
			float v = (float)i / 256f;
			for (int j = 0; j < 256; j++)
			{
				float u = (float)j / 256f;
				float num = SampleNoise(grid, 8, u, v);
				float num2 = SampleNoise(grid2, 24, u, v);
				int num3 = (int)((0.72f * num + 0.28f * num2) * 255f);
				Raylib.ImageDrawPixel(ref val, j, i, new Color(num3, num3, num3, 255));
			}
		}
		Texture2D val2 = Raylib.LoadTextureFromImage(val);
		Raylib.UnloadImage(val);
		Raylib.GenTextureMipmaps(ref val2);
		Raylib.SetTextureFilter(val2, (TextureFilter)2);
		Raylib.SetTextureWrap(val2, (TextureWrap)0);
		return val2;
	}

	private static float[] CreateNoiseGrid(int side, Random random)
	{
		float[] array = new float[side * side];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = random.NextSingle();
		}
		return array;
	}

	private static float SampleNoise(float[] grid, int side, float u, float v)
	{
		float num = u * (float)side;
		float num2 = v * (float)side;
		int num3 = (int)num;
		int num4 = (int)num2;
		int num5 = (num3 + 1) % side;
		int num6 = (num4 + 1) % side;
		float num7 = num - (float)num3;
		float num8 = num2 - (float)num4;
		num7 = num7 * num7 * (3f - 2f * num7);
		num8 = num8 * num8 * (3f - 2f * num8);
		float value = float.Lerp(grid[num4 * side + num3], grid[num4 * side + num5], num7);
		float value2 = float.Lerp(grid[num6 * side + num3], grid[num6 * side + num5], num7);
		return float.Lerp(value, value2, num8);
	}
}
