using System;
using System.IO;
using Fps.Desktop.Input;
using Fps.Desktop.Gameplay;
using Fps.Desktop.Player;
using Fps.Desktop.Rendering;
using Fps.Desktop.Scenes;
using Fps.Desktop.UI;
using Fps.Desktop.Updates;
using Raylib_cs;
using Velopack;

namespace Fps.Desktop;

internal static class Program
{
	public static void Main()
	{
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		bool flag = OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory, "sq.version"));
		if (flag)
		{
			VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
			SceneMapIO.HidePackageSupportFolders();
		}
		Raylib.SetConfigFlags((ConfigFlags)8228);
		Raylib.InitWindow(1280, 800, "Controlador FPS");
		if (!((sbyte)(Raylib.IsWindowReady()) != 0))
		{
			return;
		}
		bool audioReady = false;
		try
		{
			Raylib.InitAudioDevice();
			audioReady = Raylib.IsAudioDeviceReady();
			using PlayerAudio playerAudio = new PlayerAudio();
			Run(flag, playerAudio);
		}
		finally
		{
			if (audioReady)
				Raylib.CloseAudioDevice();
			Raylib.CloseWindow();
		}
	}

	private static void Run(bool installed, PlayerAudio playerAudio)
	{
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0178: Unknown result type (might be due to invalid IL or missing references)
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		Raylib.SetWindowMinSize(360, 260);
		Raylib.SetExitKey((KeyboardKey)0);
		Raylib.SetTargetFPS(144);
		string mapPath = SceneMapIO.DefaultPath;
        SceneDefinition initialScene;
        try { initialScene = SceneMapIO.LoadOrDefault(mapPath).ToScene(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Mapa invalido; carregando mapa padrao: " + ex.Message);
            initialScene = SceneMapIO.CreateDefault().ToScene();
        }
        DateTime mapModified = File.Exists(mapPath) ? File.GetLastWriteTimeUtc(mapPath) : DateTime.MinValue;
        float mapCheckElapsed = 0f;
        SceneManager sceneManager = new SceneManager(initialScene);
		PlayerController playerController = new PlayerController(sceneManager.Active.Spawn);
		playerController.FellFromHighPlace += playerAudio.PlayHighFallVoice;
		CollectibleManager collectibleManager = new CollectibleManager(sceneManager.Active);
		playerController.PortalTraversed += collectibleManager.NotifyPortalTraversed;
		float collectibleAnimation = 0f;
		bool completionHandled = false;
		DesktopInput desktopInput = new DesktopInput();
		FpsCamera fpsCamera = new FpsCamera();
		GameUpdates gameUpdates = (installed ? new GameUpdates() : null);
		WorldRenderer worldRenderer = new WorldRenderer(sceneManager.Active);
		using OutlinePostProcess outlinePostProcess = new OutlinePostProcess();
		using SkyRenderer skyRenderer = new SkyRenderer();
		using Hud hud = new Hud();
		while (!((sbyte)(Raylib.WindowShouldClose()) != 0))
		{
			float num = Math.Clamp(Raylib.GetFrameTime(), 0f, 0.25f);
            mapCheckElapsed += num;
            if (mapCheckElapsed >= .6f)
            {
                mapCheckElapsed = 0f;
                DateTime updated = File.Exists(mapPath) ? File.GetLastWriteTimeUtc(mapPath) : DateTime.MinValue;
                if (updated != mapModified && updated != DateTime.MinValue)
                {
                    try
                    {
                        SceneDefinition nextScene = SceneMapIO.Load(mapPath).ToScene();
                        // Build renderer before destroying the old scene to avoid partial updates.
                        WorldRenderer nextRenderer = new WorldRenderer(nextScene);
                        worldRenderer.Dispose();
                        worldRenderer = nextRenderer;
                        sceneManager.Load(nextScene);
                        playerController.PortalTraversed -= collectibleManager.NotifyPortalTraversed;
                        collectibleManager = new CollectibleManager(nextScene);
                        playerController.PortalTraversed += collectibleManager.NotifyPortalTraversed;
                        playerController.Respawn(nextScene.Spawn);
                        completionHandled = false;
                        collectibleAnimation = 0f;
                        mapModified = updated;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Falha ao atualizar mapa: " + ex.Message);
                    }
                }
            }
			collectibleAnimation += num;
			bool captured = desktopInput.Captured;
			InputState input = desktopInput.Poll();
			if (gameUpdates != null && gameUpdates.Tick(desktopInput.UpdatePressed))
			{
				break;
			}
			if (desktopInput.OutlinePressed)
			{
				outlinePostProcess.Toggle();
			}
			if (desktopInput.SkyPressed)
			{
				skyRenderer.NextTimeOfDay();
			}
			if (captured && !desktopInput.Captured)
			{
				playerController.Stop();
			}
			if (captured && desktopInput.Captured && !collectibleManager.Completed)
			{
				fpsCamera.Look(input.Mouse);
				playerController.Update(input, sceneManager.Collision, fpsCamera.Yaw, num);
			}
			else if (playerController.IsTraversing)
			{
				playerController.Update(default, sceneManager.Collision, fpsCamera.Yaw, num);
			}
			if (!playerController.IsTraversing && playerController.Elevation < -14f)
            {
                playerController.Respawn(sceneManager.Active.Spawn);
            }
			collectibleManager.Update(playerController.Feet, playerController.Elevation,
				playerController.Height, num,
				!playerController.IsTraversing && !collectibleManager.Completed);
			if (collectibleManager.Completed && !completionHandled)
			{
				playerController.Stop();
				completionHandled = true;
			}
			CameraPose pose = fpsCamera.Update(playerController, input.Mouse.X, num);
			PortalTravelView? portalView = playerController.PortalView;
			SkyLighting skyLighting = skyRenderer.Update(num);
			Raylib.BeginDrawing();
			bool flag = outlinePostProcess.BeginScene();
			Raylib.ClearBackground(WorldRenderer.Sky);
			skyRenderer.Draw(pose, skyLighting, flag);
			worldRenderer.Draw(sceneManager.Active, pose, portalView, skyLighting, collectibleManager, collectibleAnimation);
			if (flag)
			{
				outlinePostProcess.EndScene(pose.VerticalFov);
			}
			hud.Draw(playerController.Stamina.Value, num);
			if (portalView.HasValue)
			{
				PortalTravelView valueOrDefault = portalView.GetValueOrDefault();
				if (valueOrDefault.Darkness > 0f)
				{
					Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, (int)MathF.Round(valueOrDefault.Darkness * 255f)));
				}
			}
			UpdateHud.Draw(gameUpdates?.Status);
			CollectibleHud.Draw(collectibleManager);
			Raylib.EndDrawing();
		}
        worldRenderer.Dispose();
	}
}
