using System;
using System.Numerics;
using Fps.Desktop.Configuration;
using Raylib_cs;

namespace Fps.Desktop.Rendering;

internal sealed class OutlinePostProcess : IDisposable
{
	private readonly record struct SamplingUniforms(int Texel, int ClipPlanes, int ViewScale)
	{
		public SamplingUniforms(Shader shader)
			: this(Raylib.GetShaderLocation(shader, "texel"), Raylib.GetShaderLocation(shader, "clipPlanes"), Raylib.GetShaderLocation(shader, "viewScale"))
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		}

		public void Set(Shader shader, Vector2 texel, Vector2 clip, Vector2 view)
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_000e: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			Raylib.SetShaderValue<Vector2>(shader, Texel, texel, (ShaderUniformDataType)1);
			Raylib.SetShaderValue<Vector2>(shader, ClipPlanes, clip, (ShaderUniformDataType)1);
			Raylib.SetShaderValue<Vector2>(shader, ViewScale, view, (ShaderUniformDataType)1);
		}
	}

	private readonly Shader reconstruct = Raylib.LoadShaderFromMemory((string)null, "#version 330\nin vec2 fragTexCoord;\nout vec4 finalColor;\nuniform vec2 texel;\nuniform vec2 clipPlanes;\nuniform vec2 viewScale;\n\nfloat linearDepth(float depth) {\n    return clipPlanes.x * clipPlanes.y / mix(clipPlanes.y, clipPlanes.x, depth);\n}\n\nvec3 viewPosition(vec2 uv, float depth) {\n    return vec3((uv * 2.0 - 1.0) * viewScale, -1.0) * depth;\n}\nuniform sampler2D texture0;\n\nvec3 axisSlope(vec2 uv, vec2 stepUv, float raw, vec3 center) {\n    float left = texture(texture0, uv - stepUv).r;\n    float left2 = texture(texture0, uv - 2.0 * stepUv).r;\n    float right = texture(texture0, uv + stepUv).r;\n    float right2 = texture(texture0, uv + 2.0 * stepUv).r;\n    // Em uma face plana, a profundidade projetada varia linearmente.\n    // A extrapolação escolhe o lado menos afetado por uma silhueta.\n    if (abs(2.0 * left - left2 - raw) < abs(2.0 * right - right2 - raw))\n        return center - viewPosition(uv - stepUv, linearDepth(left));\n\n    return viewPosition(uv + stepUv, linearDepth(right)) - center;\n}\n\nvoid main() {\n    vec2 uv = fragTexCoord;\n    float raw = texture(texture0, uv).r;\n    if (raw >= 1.0) {\n        finalColor = vec4(0.0);\n        return;\n    }\n    vec3 point = viewPosition(uv, linearDepth(raw));\n    vec3 dx = axisSlope(uv, vec2(texel.x, 0.0), raw, point);\n    vec3 dy = axisSlope(uv, vec2(0.0, texel.y), raw, point);\n    vec3 crossSlope = cross(dx, dy);\n    float lengthSquared = dot(crossSlope, crossSlope);\n    vec3 normal = lengthSquared > 1e-16 ? crossSlope * inversesqrt(lengthSquared) : vec3(0.0, 0.0, 1.0);\n    finalColor = vec4(normal * 0.5 + 0.5, 1.0);\n}");

	private readonly Shader composite = Raylib.LoadShaderFromMemory((string)null, "#version 330\nin vec2 fragTexCoord;\nout vec4 finalColor;\nuniform vec2 texel;\nuniform vec2 clipPlanes;\nuniform vec2 viewScale;\n\nfloat linearDepth(float depth) {\n    return clipPlanes.x * clipPlanes.y / mix(clipPlanes.y, clipPlanes.x, depth);\n}\n\nvec3 viewPosition(vec2 uv, float depth) {\n    return vec3((uv * 2.0 - 1.0) * viewScale, -1.0) * depth;\n}\nuniform sampler2D texture0;\nuniform sampler2D depthMap;\nuniform sampler2D normalMap;\nuniform vec4 outlineColor;\nuniform vec4 thicknessAndDistance;\nuniform vec4 edgeThresholds;\n\nconst vec2 axes[4] = vec2[4](vec2(1, 0), vec2(0, 1), vec2(1, 1), vec2(1, -1));\n\nfloat depthAt(vec2 uv) {\n    return linearDepth(texture(depthMap, uv).r);\n}\n\nvec3 normalAt(vec2 uv, vec3 fallback) {\n    vec4 sampleNormal = texture(normalMap, uv);\n    return sampleNormal.a >= 0.5 ? sampleNormal.rgb / sampleNormal.a * 2.0 - 1.0 : fallback;\n}\n\nvoid main() {\n    vec2 uv = fragTexCoord;\n    vec4 scene = texture(texture0, uv);\n    float raw = texture(depthMap, uv).r;\n    if (raw >= 1.0) {\n        finalColor = scene;\n        return;\n    }\n\n    float center = linearDepth(raw);\n    float minimum = center;\n    float maxThickness = thicknessAndDistance.y;\n    for (int i = 0; i < 4; i++) {\n        vec2 stepUv = axes[i] * texel * maxThickness;\n        minimum = min(minimum, min(depthAt(uv - stepUv), depthAt(uv + stepUv)));\n    }\n    float opacity = 1.0 - clamp((minimum - thicknessAndDistance.z) /\n        (thicknessAndDistance.w - thicknessAndDistance.z), 0.0, 1.0);\n    if (opacity <= 0.0) {\n        finalColor = scene;\n        return;\n    }\n\n    float thickness = mix(thicknessAndDistance.x, maxThickness, opacity);\n    vec3 normal = normalAt(uv, vec3(0.0, 0.0, 1.0));\n    float curvature = 0.0;\n    float depthJump = 0.0;\n    for (int i = 0; i < 4; i++) {\n        vec2 stepUv = axes[i] * texel * thickness;\n        vec2 a = uv - stepUv;\n        vec2 b = uv + stepUv;\n        depthJump += (depthAt(a) - center) + (depthAt(b) - center);\n        vec3 difference = normalAt(a, normal) - normalAt(b, normal);\n        curvature += dot(difference, difference);\n    }\n\n    vec3 towardCamera = normalize(-viewPosition(uv, 1.0));\n    float grazing = pow(1.0 - clamp(dot(normalize(normal), towardCamera), 0.0, 1.0), 5.0);\n    float threshold = edgeThresholds.x * (1.0 + edgeThresholds.w * grazing);\n    float depthEdge = smoothstep(threshold * 0.75, threshold * 1.25, depthJump);\n    float normalEdge = smoothstep(edgeThresholds.y - edgeThresholds.z,\n        edgeThresholds.y + edgeThresholds.z, 0.5 * sqrt(curvature));\n\n    float ink = max(depthEdge, normalEdge) * outlineColor.a * opacity;\n    finalColor = vec4(mix(scene.rgb, outlineColor.rgb, ink), scene.a);\n}");

	private readonly SamplingUniforms reconstructionSampling;

	private readonly SamplingUniforms compositeSampling;

	private readonly int depthMap;

	private readonly int normalMap;

	private RenderTexture2D scene;

	private RenderTexture2D normals;

	private int width;

	private int height;

	private bool enabled = true;

	public OutlinePostProcess()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		reconstructionSampling = new SamplingUniforms(reconstruct);
		compositeSampling = new SamplingUniforms(composite);
		depthMap = Raylib.GetShaderLocation(composite, "depthMap");
		normalMap = Raylib.GetShaderLocation(composite, "normalMap");
		Raylib.SetShaderValue<Vector4>(composite, Raylib.GetShaderLocation(composite, "outlineColor"), GameSettings.Outline.Color, (ShaderUniformDataType)3);
		Raylib.SetShaderValue<Vector4>(composite, Raylib.GetShaderLocation(composite, "thicknessAndDistance"), GameSettings.Outline.ThicknessAndDistance, (ShaderUniformDataType)3);
		Raylib.SetShaderValue<Vector4>(composite, Raylib.GetShaderLocation(composite, "edgeThresholds"), GameSettings.Outline.EdgeThresholds, (ShaderUniformDataType)3);
	}

	public void Toggle()
	{
		enabled = !enabled;
	}

	public bool BeginScene()
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		if (!enabled || reconstructionSampling.Texel < 0 || depthMap < 0 || normalMap < 0)
		{
			return false;
		}
		int renderWidth = Raylib.GetRenderWidth();
		int renderHeight = Raylib.GetRenderHeight();
		if (renderWidth <= 0 || renderHeight <= 0)
		{
			return false;
		}
		if (renderWidth != width || renderHeight != height)
		{
			Resize(renderWidth, renderHeight);
		}
		if (scene.Id == 0 || normals.Id == 0)
		{
			return false;
		}
		Raylib.BeginTextureMode(scene);
		return true;
	}

	public void EndScene(float verticalFov)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		Raylib.EndTextureMode();
		Vector2 texel = new Vector2(1f / (float)width, 1f / (float)height);
		Vector2 clip = new Vector2((float)Rlgl.GetCullDistanceNear(), (float)Rlgl.GetCullDistanceFar());
		float num = MathF.Tan(verticalFov * (float)Math.PI / 360f);
		Vector2 view = new Vector2(num * (float)width / (float)height, num);
		Raylib.BeginTextureMode(normals);
		Raylib.ClearBackground(Color.Blank);
		Raylib.BeginShaderMode(reconstruct);
		reconstructionSampling.Set(reconstruct, texel, clip, view);
		DrawLayer(scene.Depth, width, height);
		Raylib.EndShaderMode();
		Raylib.EndTextureMode();
		Raylib.BeginShaderMode(composite);
		compositeSampling.Set(composite, texel, clip, view);
		Raylib.SetShaderValueTexture(composite, depthMap, scene.Depth);
		Raylib.SetShaderValueTexture(composite, normalMap, normals.Texture);
		DrawLayer(scene.Texture, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
		Raylib.EndShaderMode();
	}

	private static void DrawLayer(Texture2D source, int width, int height)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		Raylib.DrawTexturePro(source, new Rectangle(0f, 0f, (float)source.Width, (float)(-source.Height)), new Rectangle(0f, 0f, (float)width, (float)height), Vector2.Zero, 0f, Color.White);
	}

	private void Resize(int renderWidth, int renderHeight)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		Raylib.UnloadRenderTexture(normals);
		Raylib.UnloadRenderTexture(scene);
		width = renderWidth;
		height = renderHeight;
		scene = LoadSampledDepthTarget(width, height);
		normals = ((scene.Id != 0) ? Raylib.LoadRenderTexture(width, height) : default(RenderTexture2D));
		if (normals.Id != 0 && CBool.op_Implicit(Rlgl.FramebufferComplete(normals.Id)))
		{
			Raylib.SetTextureFilter(normals.Texture, (TextureFilter)1);
			Raylib.SetTextureWrap(normals.Texture, (TextureWrap)1);
		}
		else
		{
			Raylib.UnloadRenderTexture(normals);
			normals = default;
		}
	}

	private unsafe static RenderTexture2D LoadSampledDepthTarget(int width, int height)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0102: Unknown result type (might be due to invalid IL or missing references)
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010e: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_011a: Unknown result type (might be due to invalid IL or missing references)
		RenderTexture2D val = new RenderTexture2D
		{
			Id = Rlgl.LoadFramebuffer()
		};
		if (val.Id == 0)
		{
			return default;
		}
		val.Texture = new Texture2D
		{
			Id = Rlgl.LoadTexture((void*)null, width, height, (PixelFormat)7, 1),
			Format = (PixelFormat)7,
			Width = width,
			Height = height,
			Mipmaps = 1
		};
		val.Depth = new Texture2D
		{
			Id = Rlgl.LoadTextureDepth(width, height, false),
			Width = width,
			Height = height,
			Mipmaps = 1
		};
		Rlgl.FramebufferAttach(val.Id, val.Texture.Id, (FramebufferAttachType)0, (FramebufferAttachTextureType)100, 0);
		Rlgl.FramebufferAttach(val.Id, val.Depth.Id, (FramebufferAttachType)100, (FramebufferAttachTextureType)100, 0);
		if (val.Texture.Id != 0 && val.Depth.Id != 0 && CBool.op_Implicit(Rlgl.FramebufferComplete(val.Id)))
		{
			Raylib.SetTextureWrap(val.Depth, (TextureWrap)1);
			Raylib.SetTextureWrap(val.Texture, (TextureWrap)1);
			return val;
		}
		Raylib.UnloadRenderTexture(val);
		return default;
	}

	public void Dispose()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		Raylib.UnloadRenderTexture(normals);
		Raylib.UnloadRenderTexture(scene);
		Raylib.UnloadShader(composite);
		Raylib.UnloadShader(reconstruct);
	}
}
