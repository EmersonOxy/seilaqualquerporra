#define TRACE
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;

namespace Fps.Desktop.Updates;

internal sealed class GameUpdates
{
	private readonly UpdateManager? manager;

	private Task<UpdateInfo?>? check;

	private Task? download;

	private UpdateInfo? available;

	private VelopackAsset? pending;

	private bool installAfterCheck;

	private int progress;

	private string? message;

	public string? Status
	{
		get
		{
			if (download == null)
			{
				return message;
			}
			int num = Volatile.Read(in progress);
			string value = ((num >= 70) ? "Preparando atualização" : "Baixando atualização");
			return $"{value}... {num}%";
		}
	}

	public GameUpdates()
	{
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected Obj, but got Unknown
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Expected Obj, but got Unknown
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected Obj, but got Unknown
		if (!OperatingSystem.IsWindows() || !Uri.TryCreate(typeof(GameUpdates).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault((AssemblyMetadataAttribute attribute) => attribute.Key == "UpdateFeed")?.Value, UriKind.Absolute, out Uri result))
		{
			return;
		}
		IUpdateSource val2;
		if (!result.IsFile)
		{
			IUpdateSource val = (IUpdateSource)new GithubSource(result.AbsoluteUri, (string)null, false, (IFileDownloader)null);
			val2 = val;
		}
		else
		{
			IUpdateSource val = (IUpdateSource)new SimpleFileSource(new DirectoryInfo(result.LocalPath));
			val2 = val;
		}
		UpdateManager val3 = new UpdateManager(val2, (UpdateOptions)null, (IVelopackLocator)null);
		if (val3.IsInstalled)
		{
			manager = val3;
			pending = manager.UpdatePendingRestart;
			if (pending == null)
			{
				StartCheck();
				return;
			}
			message = $"Versão {pending.Version} pronta · F6 para instalar";
		}
	}

	public bool Tick(bool pressed)
	{
		if (manager == null)
		{
			return false;
		}
		Task<UpdateInfo> task = check;
		if (task != null)
		{
			installAfterCheck |= pressed;
			if (task.IsCompleted)
			{
				FinishCheck(task);
			}
			return false;
		}
		Task task2 = download;
		if (task2 != null)
		{
			if (!task2.IsCompleted)
			{
				return false;
			}
			download = null;
			try
			{
				task2.GetAwaiter().GetResult();
				pending = available.TargetFullRelease;
				return ApplyPending();
			}
			catch (Exception error)
			{
				Report(error, "Falha no download · F6 para tentar novamente");
				return false;
			}
		}
		if (!pressed)
		{
			return false;
		}
		if (pending != null)
		{
			return ApplyPending();
		}
		if (available != null)
		{
			StartDownload(available);
		}
		else
		{
			StartCheck(installWhenFound: true);
		}
		return false;
	}

	private void StartCheck(bool installWhenFound = false)
	{
		installAfterCheck = installWhenFound;
		message = (installWhenFound ? "Buscando atualização…" : null);
		check = Task.Run((Func<Task<UpdateInfo>?>)manager.CheckForUpdatesAsync);
	}

	private void FinishCheck(Task<UpdateInfo?> activeCheck)
	{
		check = null;
		try
		{
			available = activeCheck.GetAwaiter().GetResult();
			if (available == null)
			{
				message = (installAfterCheck ? "O jogo já está atualizado" : null);
				return;
			}
			if (installAfterCheck)
			{
				StartDownload(available);
				return;
			}
			message = $"Versão {available.TargetFullRelease.Version} disponível · F6 para atualizar";
		}
		catch (Exception error)
		{
			Report(error, installAfterCheck ? "Atualização indisponível · F6 para tentar novamente" : null);
		}
	}

	private void StartDownload(UpdateInfo update)
	{
		Volatile.Write(ref progress, 0);
		download = Task.Run(() => manager.DownloadUpdatesAsync(update, (Action<int>)((int percent) =>
		{
			Volatile.Write(ref progress, percent);
		}), default(CancellationToken)));
	}

	private bool ApplyPending()
	{
		try
		{
			manager.WaitExitThenApplyUpdates(pending, false, true, (string[])null);
			return true;
		}
		catch (Exception error)
		{
			Report(error, "Falha ao instalar · F6 para tentar novamente");
			return false;
		}
	}

	private void Report(Exception error, string? status)
	{
		Trace.TraceWarning("Atualização do Controlador FPS: {0}", error);
		message = status;
	}
}
