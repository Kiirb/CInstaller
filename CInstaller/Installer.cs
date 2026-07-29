using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CInstaller.entities;
using CInstaller.helpers;
using Microsoft.Win32;

namespace CInstaller;

public static class Installer
{
    
    private const int SteamGameId = 945360;
    private const string GameName = "Among us";

    public static string? FindSteamPath()
    {
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString();
    }

    public static (string, string) FindGameFolder(string steamPath)
    {
        string libraryFolderFile = Path.Join(steamPath, "steamapps", "libraryfolders.vdf");
        Regex regex = new Regex("\"path\"\\s+\"([^\"]+)\"[\\s\\S]*?\"apps\"\\s*\\{([\\s\\S]*?)\\}", RegexOptions.Multiline);
        
        foreach (Match lib in regex.Matches(File.ReadAllText(libraryFolderFile)))
        {
            string path = lib.Groups[1].Value.Replace("\\\\", "\\");
            string appsBlock = lib.Groups[2].Value;

            if (Regex.IsMatch(appsBlock, $"\"{SteamGameId}\""))
            {
                string steamCommon = Path.Join(path, "steamapps", "common");
                
                string gameFolder = Path.Join(steamCommon, GameName);

                if (!Directory.Exists(gameFolder))
                {
                    Console.Write($"{gameFolder} doesn't exist");
                    gameFolder = "";
                }
        
                return (steamCommon, gameFolder);
            }
        }
        Console.Out.WriteLine("SteamCommon not found");
        return ("", "");
    }
    
    public static async Task<string> RunInstaller(ProgressReporter progress, string steamCommon, string gameFolder)
    {
        string moddedFolder = Path.Join(steamCommon, GameName + " Modded");
        
        progress.NextStep(20);
        await Task.Run(() =>
        {
            Utils.CopyDirectory(gameFolder, moddedFolder, progress);
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
        if (!Directory.Exists(pluginFolder))
        {
            Console.Write(pluginFolder + " not found");
            //maybe throw error here instead
            return "";
        }
                
        File.Delete(Path.Join(pluginFolder, "AUnlocker.dll"));
        List<GithubRepo> plugins = await RemoteManager.GetPluginConfig();

        int stepPerPlugin = 30 / plugins.Count;

        foreach (GithubRepo plugin in plugins)
        {
            progress.NextStep(stepPerPlugin);
            string pluginUrl = await RemoteManager.FindLatestGithubDownloadAsset(plugin.RepoOwner, plugin.RepoName, plugin.SearchPattern);
            await RemoteManager.DownloadFile(pluginUrl, pluginFolder, progress);
            progress.FinishStep();
        }
        
        Console.Out.WriteLine("Finished downloads");

        return moddedFolder;
    }

    public static async Task<bool> AddToSteam(string steamPath, string moddedFolder, ProgressReporter progress)
    {
        string moddedExeFilePath = Path.Join(moddedFolder, GameName + ".exe");
        
        long currentSteamUserId = SteamManager.FindCurrentSteamUserId(steamPath);
        string iconPath = await GetCustomAssets(steamPath, currentSteamUserId, progress);
        return SteamManager.AddShortcut(steamPath, moddedFolder, moddedExeFilePath, iconPath, currentSteamUserId);
    }

    public static async Task CleanUpGameFiles(string steamPath, string steamCommon)
    {
        foreach (string dir in Directory.GetDirectories(steamCommon, $"{GameName}*", SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(dir, true);
        }
        
        await SteamManager.LaunchSteam(steamPath);
        
        Process.Start(new ProcessStartInfo($"steam://validate/{SteamGameId}") { UseShellExecute = true });

        await TrackGameDownload(steamCommon);
    }
    
    private static async Task TrackGameDownload(string steamCommon)
    {
        var gameDownloadFolder = Path.Join(Directory.GetParent(steamCommon)?.ToString(), "downloading", SteamGameId.ToString());
        
        await Task.Delay(3000);
        
        while (Directory.Exists(gameDownloadFolder))
        {
            await Task.Delay(3000);
        }
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
        
        foreach (var asset in assets)
        {
            progress?.NextStep(stepPerAsset);
            var grid = await RemoteManager.DownloadFile(asset.url, gridFolderPath, progress);
            var renamedGrid = Path.Join(gridFolderPath, steamGridId + asset.fileEnding);
            File.Move(grid,  renamedGrid, true);
            progress?.FinishStep();

            if (asset == assets.Last()) return renamedGrid;
        }

        return string.Empty;
    }

    public static int getSteamGameId()
    {
        return SteamGameId;
    }
}