using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using CInstaller.entities;
using CInstaller.helpers;
using CInstaller.ui;

namespace CInstaller;

public static class Loader
{
    public static async Task LoaderRun()
    {
        CheckMissingConfigs();

        try
        {
            await UpdatePlugins();
        }
        catch {}
        
        ConfigHandler.Save();
        
        string gameExePath = Path.Join(ConfigHandler.GameFolder, SteamManager.GameName + ".exe");
        
        if (File.Exists(gameExePath))
        {
            Process.Start(gameExePath);
        }
        else
        {
            MessageBox.Show(
                $"Game exe nicht gefunden:\n\nPath:{gameExePath}", 
                "Not Found", 
                MessageBoxButton.OK, 
                MessageBoxImage.Error);
        }
        Application.Current.Shutdown();
    }

    private static async Task UpdatePlugins()
    {
        List<(GithubRepo, string)> toUpdateList = [];
        
        List<GithubRepo> remotePluginList = await RemoteManager.GetPluginConfig();

        HashSet<string> remoteRepoNames = [.. remotePluginList.Select(r => r.RepoName)];
        ConfigHandler.PluginList.RemoveAll(configPlugin => !remoteRepoNames.Contains(configPlugin.RepoName));
        
        foreach (GithubRepo remotePlugin in remotePluginList)
        {
            GithubRepo? configPlugin = ConfigHandler.PluginList.Find(u => u.RepoName == remotePlugin.RepoName);
            HttpResponseMessage githubResponse = await RemoteManager.FindLatestGithubRelease(remotePlugin.RepoOwner, remotePlugin.RepoName);
            Version? LatestPluginVersion = await RemoteManager.ExtractVersionFromResponse(githubResponse);
            if (configPlugin != null && File.Exists(configPlugin.filePath))
            {
                if (LatestPluginVersion != null && LatestPluginVersion <= configPlugin.version)
                {
                    continue;
                }
                File.Delete(configPlugin.filePath);
                ConfigHandler.PluginList.Remove(configPlugin);
            }
            
            remotePlugin.version = LatestPluginVersion!;
            
            string assetUrl = await RemoteManager.FindLatestGithubDownloadAsset(remotePlugin.RepoOwner, remotePlugin.RepoName, remotePlugin.SearchPattern, githubResponse);
            toUpdateList.Add((remotePlugin, assetUrl));
        }

        string pluginFolder = Path.Join(ConfigHandler.GameFolder, "BepInEx", "plugins");
        if (!Directory.Exists(pluginFolder)) Directory.CreateDirectory(pluginFolder);
        if (toUpdateList.Any())
        {
            ProgressUI progressUi = new();
            progressUi.Show();
            
            int stepPerPlugin = 100 / toUpdateList.Count;
            foreach ((GithubRepo remotePlugin, string pluginUrl) in toUpdateList)
            {
                progressUi.Progress.NextStep(stepPerPlugin);
                string pluginFilePath = await RemoteManager.DownloadFile(pluginUrl, pluginFolder, progressUi.Progress);
                remotePlugin.filePath = pluginFilePath;
                ConfigHandler.PluginList.Add(remotePlugin);
                progressUi.Progress.FinishStep();
            }
        }
    }

    private static void CheckMissingConfigs()
    {
        if (string.IsNullOrWhiteSpace(ConfigHandler.SteamFolder) || !Directory.Exists(ConfigHandler.SteamFolder)) 
            ConfigHandler.SteamFolder = SteamManager.FindSteamPath()!;

        if (string.IsNullOrEmpty(ConfigHandler.SteamCommon) || !Directory.Exists(ConfigHandler.SteamCommon))
            ConfigHandler.SteamCommon = SteamManager.FindSteamCommon(ConfigHandler.SteamFolder);

        if (string.IsNullOrWhiteSpace(ConfigHandler.GameFolder) || !Directory.Exists(ConfigHandler.GameFolder))
            ConfigHandler.GameFolder = Path.Join(ConfigHandler.SteamCommon, SteamManager.GameName + " Modded");
        
        ConfigHandler.Save();
    }
}