using System;
using System.Numerics;
using Fps.Desktop.Configuration;
using Fps.Desktop.Player;
using Raylib_cs;

namespace Fps.Desktop.Rendering;

internal sealed class SkyRenderer : IDisposable
{
	private readonly Shader shader = Raylib.LoadShaderFromMemory((string)null, "#version 330\nout vec4 finalColor;\nuniform sampler2D cloudMap;\nuniform vec3 cameraForward;\nuniform vec3 cameraRight;\nuniform vec3 cameraUp;\nuniform vec4 pixelToRay;\nuniform vec3 sunDirection;\nuniform vec3 moonDirection;\nuniform vec3 skyZenith;\nuniform vec3 skyHorizon;\nuniform vec3 belowHorizon;\nuniform vec3 conditions;\nuniform vec4 sizesAndMotion;\n\nconst float PI = 3.14159265;\n\nfloat starHash(ivec2 cell) {\n    uvec2 n = uvec2(cell);\n    uint h = (n.x * 0x8da6b343u) ^ (n.y * 0xd8163841u);\n    h ^= h >> 16;\n    h *= 0x7feb352du;\n    h ^= h >> 15;\n    return float(h & 65535u) / 65535.0;\n}\n\nfloat stars(vec2 uv, float time) {\n    float rotation = time * sizesAndMotion.w;\n    float c = cos(rotation);\n    float s = sin(rotation);\n    vec2 grid = (mat2(c, s, -s, c) * uv) * 66.0;\n    ivec2 cell = ivec2(floor(grid));\n    float seed = starHash(cell);\n    vec2 offset = vec2(starHash(cell + ivec2(74, 19)), starHash(cell + ivec2(126, 11)));\n    float distanceToStar = length(fract(grid) - mix(vec2(0.24), vec2(0.76), offset));\n    float size = mix(0.085, 0.22, seed);\n    float glow = 1.0 - smoothstep(size * 0.22, size, distanceToStar);\n    float twinkle = 0.78 + 0.22 * sin(seed * 93.0 + time * 1.1);\n    return step(0.965, seed) * glow * twinkle;\n}\n\nvoid main() {\n    vec2 screen = (gl_FragCoord.xy * pixelToRay.xy * 2.0 - 1.0) * pixelToRay.zw;\n    vec3 ray = normalize(cameraForward + cameraRight * screen.x + cameraUp * screen.y);\n    float sunset = conditions.x;\n    float night = conditions.y;\n    float time = conditions.z;\n    float height = abs(sin(ray.y * PI * 0.5));\n    vec3 color = mix(skyHorizon, skyZenith, height);\n    float sunDistance = length(ray - sunDirection);\n    float west = pow(max(0.0, 1.0 - sunDistance * sunDistance), 2.0);\n    color = mix(color, vec3(1.0, 0.52, 0.40), west * sunset * (1.0 - night) * (1.0 - height) * 0.42);\n    float horizonMask = 1.0 - smoothstep(0.0, 0.11, -ray.y);\n    color = mix(belowHorizon, color, horizonMask);\n\n    vec2 skyUv = ray.xz / sqrt(max(ray.y, 0.02));\n    float moonRadius = sizesAndMotion.y * (1.0 + cos(moonDirection.y * PI) * 0.25);\n    float moonDistance = length(ray - moonDirection);\n    float moonDisk = (1.0 - smoothstep(moonRadius * 0.93, moonRadius, moonDistance)) * horizonMask;\n    if (night > 0.01 && ray.y > 0.0)\n        color += vec3(0.86, 0.90, 1.0) * stars(skyUv, time) * night *\n            smoothstep(0.025, 0.16, ray.y) * (1.0 - moonDisk) * 0.72;\n\n    float sunRadius = sizesAndMotion.x * (1.0 + cos(sunDirection.y * PI) * 0.25);\n    float sunDisk = 1.0 - smoothstep(sunRadius * 0.87, sunRadius, sunDistance);\n    float halo = exp(-sunDistance * sunDistance / (sunRadius * sunRadius * 2.9));\n    vec3 sunTint = mix(vec3(1.0, 0.94, 0.62), vec3(1.0, 0.39, 0.16), sunset);\n    float sunVisibility = smoothstep(-0.11, 0.015, sunDirection.y) * horizonMask * (1.0 - moonDisk);\n    color += sunTint * halo * 0.32 * sunVisibility;\n    color = mix(color, mix(sunTint, vec3(1.0), 0.52), sunDisk * sunVisibility);\n\n    if (moonDisk > 0.0) {\n        // A interseção do raio com a esfera dá a normal para a fase lunar.\n        float projection = dot(ray, moonDirection);\n        float chord = sqrt(max(0.0, projection * projection - 1.0 + moonRadius * moonRadius));\n        vec3 normal = normalize(ray * (projection - chord) - moonDirection);\n        float phase = pow(clamp(dot(normal, sunDirection), 0.04, 1.0), 1.45);\n        vec3 unlit = mix(color * 0.88, vec3(0.075, 0.084, 0.15), night * 0.62);\n        color = mix(color, mix(unlit, vec3(1.0, 0.95, 0.76), phase), moonDisk);\n    }\n\n    if (ray.y > 0.0) {\n        vec2 wind = vec2(0.95, 0.31) * time * sizesAndMotion.z;\n        vec2 plane = skyUv * 0.16;\n        float low = smoothstep(0.44, 0.65,\n            texture(cloudMap, plane * 1.2 - wind * 0.079).b);\n        float middle = smoothstep(0.47, 0.68,\n            texture(cloudMap, plane * 0.91 - wind * 0.089).g + low * 0.045);\n        float high = smoothstep(0.49, 0.71,\n            texture(cloudMap, plane * 0.78 - wind * 0.10).r + middle * 0.05);\n        float cloudMask = max(high, max(middle * 0.81, low * 0.66));\n        cloudMask *= smoothstep(0.045, 0.24, ray.y);\n        vec3 cloud = mix(vec3(0.81, 0.84, 0.97), vec3(1.0), max(high, middle * 0.43));\n        cloud = mix(cloud, vec3(1.0, 0.64, 0.61), sunset * (1.0 - night) * 0.65);\n        cloud = mix(cloud, skyZenith * 1.65 + vec3(0.07, 0.08, 0.11), night * 0.93);\n        color = mix(color, cloud, cloudMask * mix(0.91, 0.72, night));\n    }\n\n    finalColor = vec4(clamp(color, 0.0, 1.0), 1.0);\n}");

	private readonly Texture2D clouds;

	private readonly int cloudMap;

	private readonly int forward;

	private readonly int right;

	private readonly int up;

	private readonly int pixelToRay;

	private readonly int sun;

	private readonly int moon;

	private readonly int zenith;

	private readonly int horizon;

	private readonly int fog;

	private readonly int conditions;

	private readonly int sizesAndMotion;

	private int phase;

	private Vector3 sunDirection = GameSettings.Sky.DaySun;

	public SkyRenderer()
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0114: Unknown result type (might be due to invalid IL or missing references)
		cloudMap = Location("cloudMap");
		forward = Location("cameraForward");
		right = Location("cameraRight");
		up = Location("cameraUp");
		pixelToRay = Location("pixelToRay");
		sun = Location("sunDirection");
		moon = Location("moonDirection");
		zenith = Location("skyZenith");
		horizon = Location("skyHorizon");
		fog = Location("belowHorizon");
		conditions = Location("conditions");
		sizesAndMotion = Location("sizesAndMotion");
		if (cloudMap >= 0)
		{
			clouds = CreateCloudMap();
			Set(moon, GameSettings.Sky.Moon);
			Raylib.SetShaderValue<Vector4>(shader, sizesAndMotion, new Vector4(0.075f, 0.06f, 0.018f, 0.004f), (ShaderUniformDataType)3);
		}
	}

	public void NextTimeOfDay()
	{
		phase = (phase + 1) % 3;
	}

	public SkyLighting Update(float dt)
	{
		Vector3 value = phase switch
		{
			1 => GameSettings.Sky.SunsetSun, 
			2 => GameSettings.Sky.NightSun, 
			_ => GameSettings.Sky.DaySun, 
		};
		sunDirection = Vector3.Lerp(sunDirection, value, MathEx.Damp(0f, 1f, 4.8f, dt));
		Vector3 vector = Vector3.Normalize(sunDirection);
		float num = 1f - Smooth(-0.5f, 0.04f, vector.Y);
		float num2 = 1f - Smooth(0.02f, 0.47f, MathF.Abs(vector.Y));
		Vector3 vector2 = Vector3.Lerp(Vector3.Lerp(GameSettings.Sky.DayTop, GameSettings.Sky.SunsetTop, num2), GameSettings.Sky.NightTop, num);
		Vector3 vector3 = Vector3.Lerp(Vector3.Lerp(GameSettings.Sky.DayHorizon, GameSettings.Sky.SunsetHorizon, num2), GameSettings.Sky.NightHorizon, num);
		Vector3 vector4 = vector3 * new Vector3(0.78f, 0.87f, 0.9f);
		Vector3 direction = Vector3.Normalize(Vector3.Lerp(Vector3.Normalize(new Vector3(vector.X, MathF.Max(0.11f, vector.Y), vector.Z)), GameSettings.Sky.Moon, num));
		return new SkyLighting(vector, direction, vector2, vector3, vector4, num2, num);
	}

	public void Draw(CameraPose pose, SkyLighting lighting, bool offscreen)
	{
		//IL_0090: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0147: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		if (cloudMap >= 0 && clouds.Id != 0)
		{
			int renderWidth = Raylib.GetRenderWidth();
			int renderHeight = Raylib.GetRenderHeight();
			if (renderWidth > 0 && renderHeight > 0)
			{
				float num = MathF.Tan(pose.VerticalFov * (float)Math.PI / 360f);
				Set(forward, pose.Forward);
				Set(right, Vector3.Normalize(Vector3.Cross(pose.Forward, pose.Up)));
				Set(up, pose.Up);
				Raylib.SetShaderValue<Vector4>(shader, pixelToRay, new Vector4(1f / (float)renderWidth, 1f / (float)renderHeight, num * (float)renderWidth / (float)renderHeight, num), (ShaderUniformDataType)3);
				Set(sun, lighting.Sun);
				Set(zenith, lighting.Zenith);
				Set(horizon, lighting.Horizon);
				Set(fog, lighting.Fog);
				Set(conditions, new Vector3(lighting.Sunset, lighting.Night, (float)Raylib.GetTime()));
				Raylib.BeginShaderMode(shader);
				Raylib.SetShaderValueTexture(shader, cloudMap, clouds);
				Raylib.DrawRectangle(0, 0, offscreen ? renderWidth : Raylib.GetScreenWidth(), offscreen ? renderHeight : Raylib.GetScreenHeight(), Color.White);
				Raylib.EndShaderMode();
			}
		}
	}

	public void Dispose()
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		if (clouds.Id != 0)
		{
			Raylib.UnloadTexture(clouds);
		}
		Raylib.UnloadShader(shader);
	}

	private int Location(string name)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		return Raylib.GetShaderLocation(shader, name);
	}

	private void Set(int uniform, Vector3 value)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		Raylib.SetShaderValue<Vector3>(shader, uniform, value, (ShaderUniformDataType)2);
	}

	private static float Smooth(float low, float high, float value)
	{
		float num = Math.Clamp((value - low) / (high - low), 0f, 1f);
		return num * num * (3f - 2f * num);
	}

	private unsafe static Texture2D CreateCloudMap()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0210: Unknown result type (might be due to invalid IL or missing references)
		Image val = Raylib.GenImageColor(512, 512, Color.White);
		if (val.Data == null)
		{
			return default;
		}
		Span<byte> span = new Span<byte>(val.Data, 1048576);
		Random random = new Random(7219);
		float[] array = new float[262144];
		for (int i = 0; i < 3; i++)
		{
			Array.Clear(array);
			float num = 1f;
			float num2 = 0f;
			for (int num3 = 8; num3 <= 64; num3 *= 2)
			{
				float[] array2 = new float[num3 * num3];
				for (int j = 0; j < array2.Length; j++)
				{
					array2[j] = random.NextSingle();
				}
				int num4 = 512 / num3;
				int num5 = num3 - 1;
				for (int k = 0; k < 512; k++)
				{
					int num6 = k / num4;
					int num7 = (num6 + 1) & num5;
					float amount = Smooth(0f, num4, k % num4);
					for (int l = 0; l < 512; l++)
					{
						int num8 = l / num4;
						int num9 = (num8 + 1) & num5;
						float amount2 = Smooth(0f, num4, l % num4);
						float value = float.Lerp(array2[num6 * num3 + num8], array2[num6 * num3 + num9], amount2);
						float value2 = float.Lerp(array2[num7 * num3 + num8], array2[num7 * num3 + num9], amount2);
						array[k * 512 + l] += num * float.Lerp(value, value2, amount);
					}
				}
				num2 += num;
				num *= 0.48f;
			}
			for (int m = 0; m < array.Length; m++)
			{
				span[m * 4 + i] = (byte)MathF.Round(array[m] / num2 * 255f);
			}
		}
		Texture2D val2 = Raylib.LoadTextureFromImage(val);
		Raylib.UnloadImage(val);
		if (val2.Id == 0)
		{
			return default;
		}
		Raylib.GenTextureMipmaps(ref val2);
		Raylib.SetTextureFilter(val2, (TextureFilter)2);
		Raylib.SetTextureWrap(val2, (TextureWrap)0);
		return val2;
	}
}
