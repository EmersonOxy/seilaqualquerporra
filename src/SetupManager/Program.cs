using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
#if EDITOR_SETUP
    private const bool IsEditor = true;
#else
    private const bool IsEditor = false;
#endif

    private const string Repository = "https://github.com/EmersonOxy/seilaqualquerporra";

    [STAThread]
    private static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var appName = IsEditor ? "Editor de Mapas - Controlador FPS" : "Controlador FPS";
        var appExe = IsEditor ? "MapEditor.exe" : "ControladorFPS.exe";
        var setupAsset = IsEditor ? "MapEditor-editor-Setup.exe" : "ControladorFPS-win-Setup.exe";
        var baseDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        var installDir = Path.Combine(baseDir, "Aplicativo");
        var cacheDir = Path.Combine(baseDir, "Cache");
        var cacheSetup = Path.Combine(cacheDir, setupAsset);
        var setupUrl = Repository + "/releases/latest/download/" + setupAsset;

        var hasInstall = File.Exists(Path.Combine(installDir, "Update.exe")) &&
                         File.Exists(Path.Combine(installDir, "current", "sq.version"));
        var action = "install";
        if (hasInstall)
        {
            var result = MessageBox.Show(
                appName + " ja esta instalado.\n\nSim: atualizar para a ultima versao do GitHub.\nNao: restaurar com o ultimo setup salvo.\nCancelar: sair.",
                "Setup - " + appName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
            if (result == DialogResult.Cancel) return;
            action = result == DialogResult.Yes ? "update" : "restore";
        }

        Application.Run(new SetupProgress(appName, appExe, setupAsset, setupUrl,
            installDir, cacheDir, cacheSetup, action));
    }
}

internal sealed class SetupProgress : Form
{
    private readonly string appName;
    private readonly string appExe;
    private readonly string setupAsset;
    private readonly string setupUrl;
    private readonly string installDir;
    private readonly string cacheDir;
    private readonly string cacheSetup;
    private readonly string action;
    private readonly Label status;
    private bool started;

    public SetupProgress(string appName, string appExe, string setupAsset,
        string setupUrl, string installDir, string cacheDir, string cacheSetup, string action)
    {
        this.appName = appName;
        this.appExe = appExe;
        this.setupAsset = setupAsset;
        this.setupUrl = setupUrl;
        this.installDir = installDir;
        this.cacheDir = cacheDir;
        this.cacheSetup = cacheSetup;
        this.action = action;

        Text = "Setup - " + appName;
        ClientSize = new Size(460, 135);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;

        status = new Label
        {
            AutoSize = false,
            Location = new Point(20, 22),
            Size = new Size(420, 30),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var progress = new ProgressBar
        {
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 22,
            Location = new Point(20, 70),
            Size = new Size(420, 20)
        };
        Controls.Add(status);
        Controls.Add(progress);
        Shown += async delegate { await RunAsync(); };
    }

    private async Task RunAsync()
    {
        if (started) return;
        started = true;
        try
        {
            Directory.CreateDirectory(cacheDir);
            string setupPath;
            if (action == "restore" && File.Exists(cacheSetup))
            {
                setupPath = cacheSetup;
                status.Text = "Restaurando " + appName + "...";
            }
            else
            {
                status.Text = action == "update" ? "Buscando a ultima versao no GitHub..." : "Baixando e instalando " + appName + "...";
                setupPath = Path.Combine(cacheDir, setupAsset + ".download.exe");
                if (File.Exists(setupPath)) File.Delete(setupPath);
                using (var client = new WebClient())
                    await client.DownloadFileTaskAsync(new Uri(setupUrl), setupPath);
            }

        var installArgs = "--installto " + Quote(installDir) + " --silent";
        var removedOldInstall = false;
        Exception installError = null;
        try
            {
            if (action != "install")
            {
                var updater = Path.Combine(installDir, "Update.exe");
                if (File.Exists(updater))
                {
                    status.Text = "Preparando a instalacao...";
                    await RunProcessAsync(updater, "--rootDir " + Quote(installDir) + " uninstall --silent");
                    removedOldInstall = true;
                }
            }

            status.Text = action == "install" ? "Instalando " + appName + "..." : "Aplicando a versao escolhida...";
            await RunProcessAsync(setupPath, installArgs);
        }
        catch (Exception updateError)
        {
            installError = updateError;
        }

        if (installError != null)
        {
            if (removedOldInstall && action == "update" && File.Exists(cacheSetup))
            {
                try
                {
                    status.Text = "Restaurando a versao anterior...";
                    await RunProcessAsync(cacheSetup, installArgs);
                    MessageBox.Show("A atualizacao falhou. A versao anterior foi restaurada.\n\n" + installError.Message,
                        Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Close();
                    return;
                }
                catch (Exception restoreError)
                {
                    throw new InvalidOperationException("A atualizacao e a restauracao automatica falharam. Rode Setup.exe novamente e escolha Restaurar.",
                        new AggregateException(installError, restoreError));
                }
            }

            throw new InvalidOperationException("Nao foi possivel concluir a instalacao.", installError);
            }

            if (action != "restore" || !File.Exists(cacheSetup))
            {
                if (File.Exists(cacheSetup)) File.Delete(cacheSetup);
                File.Move(setupPath, cacheSetup);
            }

            if (action == "install")
            {
                var installedExe = Path.Combine(installDir, appExe);
                if (File.Exists(installedExe)) Process.Start(installedExe);
                status.Text = appName + " instalado.";
            }
            else if (action == "update")
            {
                status.Text = appName + " atualizado. O aplicativo nao foi aberto.";
            }
            else
            {
                status.Text = appName + " restaurado.";
            }

            await Task.Delay(1200);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Nao foi possivel concluir. Verifique a conexao com a internet e feche o aplicativo se ele estiver aberto.\n\n" + ex.Message,
                Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private async Task RunProcessAsync(string executable, string arguments)
    {
        var start = new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = true,
            WorkingDirectory = cacheDir
        };
        using (var process = Process.Start(start))
        {
            if (process == null) throw new InvalidOperationException("Nao foi possivel iniciar o instalador.");
            await Task.Run(delegate { process.WaitForExit(); });
            if (process.ExitCode != 0)
                throw new InvalidOperationException("O instalador terminou com o codigo " + process.ExitCode + ".");
        }
    }
}
