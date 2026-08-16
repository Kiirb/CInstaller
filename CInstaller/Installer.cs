using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using CInstaller.entities;
using CInstaller.helpers;
using CInstaller.ui;

namespace CInstaller;

public static class Installer
{
    public static async Task InstallerRun()
    {
        ProgressUI progressUi = new();
        progressUi.Show();
        
        string? steamPath = SteamManager.FindSteamPath();
        if (string.IsNullOrEmpty(steamPath))
        {
            MessageBox.Show(
                "Steam konnte nicht gefunden werden",
                "Find Path Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
            return;
        }

        string steamCommon = SteamManager.FindSteamCommon(steamPath);

        if (string.IsNullOrEmpty(steamCommon) || !Directory.Exists(Path.Join(steamCommon, SteamManager.GameName)))
        {
            MessageBoxResult installGameFlag = MessageBox.Show(
                "Among us ist nicht installiert, installiere es und starte den Installer neu",
                "Not installed",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (installGameFlag.Equals(MessageBoxResult.OK))
            {
                await SteamManager.InstallGame(steamPath, SteamManager.SteamGameId);
            }
        }
        
        progressUi.Progress.Report(0, "Starte Installer");
        string moddedFolder = await Install(progressUi.Progress, steamCommon);
        if (string.IsNullOrEmpty(moddedFolder))
        {
            MessageBox.Show(
                "Plugin Ordner konnte nicht gefunden werden",
                "Install Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        
        bool restartSteamFlag = await AddToSteam(steamPath, moddedFolder, progressUi.Progress);

        ConfigSave(steamPath, steamCommon, moddedFolder);
        
        progressUi.Progress.Complete("Installation complete!");

        if (restartSteamFlag && SteamManager.IsSteamRunning())
        {
            MessageBoxResult result = MessageBox.Show(
                "Steam Muss neu gestartet werden um alle änderungen zu übernehmen\n\nJetzt neustarten?",
                "Confirmation",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.OK)
            {
                await SteamManager.RestartSteam(steamPath);
            }
        }
        else
        {
            MessageBox.Show(
                "Installation Fertig!",
                "Install Done",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        
        Application.Current.Shutdown();
    }

    private static void ConfigSave(string steamFolder, string steamCommon, string gameFolder)
    {
        ConfigHandler.SteamFolder = steamFolder;
        ConfigHandler.SteamCommon = steamCommon;
        ConfigHandler.GameFolder = gameFolder;
        ConfigHandler.Save(gameFolder);
    }

    private static async Task<string> Install(ProgressReporter progress, string steamCommon)
    {
        string gameFolder = Path.Join(steamCommon, SteamManager.GameName);
        string moddedFolder = Path.Join(steamCommon, SteamManager.GameName + " Modded");
        
        progress.NextStep(20);
        await Task.Run(() =>
        {
            Utils.CopyDirectoryWithoutBepInEx(gameFolder, moddedFolder, progress);
        });
        progress.FinishStep();
        
        progress.NextStep(30);
        string baseModUrl = await RemoteManager.FindLatestGithubDownloadAsset("AU-Avengers", "TOU-Mira", "-steam-itch.zip");
        string filePath = await RemoteManager.DownloadFile(baseModUrl, moddedFolder, progress);
        progress.FinishStep();
        
        progress.NextStep(10);
        await Task.Run(() =>
        {
            Utils.ExtractZip(filePath, moddedFolder, progress);
        });
        File.Delete(filePath);
        progress.FinishStep();
        
        string pluginFolder = Path.Join(moddedFolder, "BepinEx", "plugins");
        if (!Directory.Exists(pluginFolder)) Directory.CreateDirectory(pluginFolder);
        File.Delete(Path.Join(pluginFolder, "AUnlocker.dll"));
        
        List<GithubRepo> plugins = await RemoteManager.GetPluginConfig();

        int stepPerPlugin = 27 / plugins.Count;
        
        foreach (GithubRepo plugin in plugins)
        {
            progress.NextStep(stepPerPlugin);
            HttpResponseMessage pluginResponse = await RemoteManager.FindLatestGithubRelease(plugin.RepoOwner, plugin.RepoName);
            Version? pluginVersion = await RemoteManager.ExtractVersionFromResponse(pluginResponse);
            string pluginUrl = await RemoteManager.FindLatestGithubDownloadAsset(plugin.RepoOwner, plugin.RepoName, ".dll", pluginResponse);
            string pluginFilePath = await RemoteManager.DownloadFile(pluginUrl, pluginFolder, progress);

            plugin.version = pluginVersion!;
            plugin.filePath = pluginFilePath;
            ConfigHandler.PluginList.Add(plugin);
            progress.FinishStep();
        }
        
        progress.NextStep(3);
        string projectName = Assembly.GetExecutingAssembly().GetName().Name!;
        string loaderFile = Path.Join(moddedFolder, projectName + ".exe");
        if (!File.Exists(loaderFile))
        {
            string cinstallerAsset = await RemoteManager.FindLatestGithubDownloadAsset("Kiirb", projectName, ".exe");
            string cinstallerFile = await RemoteManager.DownloadFile(cinstallerAsset, moddedFolder, progress);
            File.Move(cinstallerFile, loaderFile);
        }
        progress.FinishStep();

        Console.Out.WriteLine("Finished downloads");
        
        return moddedFolder;
    }

    private static async Task<bool> AddToSteam(string steamPath, string moddedFolder, ProgressReporter progress)
    {
        string launchExeFilePath = Path.Join(moddedFolder, Assembly.GetExecutingAssembly().GetName().Name + ".exe");
        long currentSteamUserId = SteamManager.FindCurrentSteamUserId(steamPath);
        string iconPath = await GetCustomAssets(steamPath, currentSteamUserId, progress);
        return SteamManager.AddShortcut(steamPath, moddedFolder, launchExeFilePath, iconPath, currentSteamUserId);
    }
    
    private static async Task<string> GetCustomAssets(string steamPath, long currentSteamUserId, ProgressReporter progress)
    {
        string gridFolderPath = Path.Join(steamPath, "userdata", currentSteamUserId.ToString(), "config", "grid");
        if (!Directory.Exists(gridFolderPath)) Directory.CreateDirectory(gridFolderPath);
            
        const string steamGridId = "4294662226"; //only if game id is -305070
        List<(string url, string fileEnding)> assets =
        [
            ("https://cdn2.steamgriddb.com/grid/24330531679f7fd5318e3e9dde4e1c99.png", "p.png"),
            ("https://cdn2.steamgriddb.com/hero/1cc1fab198176208789cf94b71412dc8.png", "_hero.png"),
            ("https://cdn2.steamgriddb.com/logo/1d92bf06b68f0b08837d6d88412df8ec.png", "_logo.png"),
            ("https://cdn2.steamgriddb.com/icon/588bc7654c8815a85a09b0bc6d82a29f.png", "_icon.png")
        ];

        int stepPerAsset = 10 / assets.Count;
        
        const string logoConfig =
            "{\"nVersion\":1,\"logoPosition\":{\"pinnedPosition\":\"CenterCenter\",\"nWidthPct\":23.704171934260415,\"nHeightPct\":65.2777777777778}}"; //make actual json reader/write if i feel like it
        File.WriteAllText(Path.Join(gridFolderPath, $"{steamGridId}.json"), logoConfig);
        
        foreach ((string url, string fileEnding) asset in assets)
        {
            progress?.NextStep(stepPerAsset);
            string grid = await RemoteManager.DownloadFile(asset.url, gridFolderPath, progress);
            string renamedGrid = Path.Join(gridFolderPath, steamGridId + asset.fileEnding);
            File.Move(grid,  renamedGrid, true);
            progress?.FinishStep();

            if (asset == assets.Last()) return renamedGrid;
        }

        return string.Empty;
    }
}