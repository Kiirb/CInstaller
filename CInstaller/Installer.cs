using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CInstaller;

public static class Installer
{
    private static ProgressReporter? _progress;

    
    private const int SteamGameId = 945360;
    private const string GameName = "Among us";

    public static string? FindSteamPath()
    {
        return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString();
    }

    public static (string, string) FindGameFolder(string steamPath)
    {
        var libraryFolderFile = Path.Join(steamPath, "steamapps", "libraryfolders.vdf");
        var regex = new Regex("\"path\"\\s+\"([^\"]+)\"[\\s\\S]*?\"apps\"\\s*\\{([\\s\\S]*?)\\}", RegexOptions.Multiline);
        
        foreach (Match lib in regex.Matches(File.ReadAllText(libraryFolderFile)))
        {
            var path = lib.Groups[1].Value.Replace("\\\\", "\\");
            var appsBlock = lib.Groups[2].Value;

            if (Regex.IsMatch(appsBlock, $"\"{SteamGameId}\""))
            {
                var steamCommon = Path.Join(path, "steamapps", "common");
                
                var gameFolder = Path.Join(steamCommon, GameName);

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
        _progress = progress;
        
        var moddedFolder = Path.Join(steamCommon, GameName + " Modded");
        
        _progress.NextStep(20);
        await Task.Run(() =>
        {
            CopyDirectory(gameFolder, moddedFolder, progress);
        });
        _progress.FinishStep();
        
        _progress.NextStep(30);
        var baseModUrl = await GithubManager.FindLatestGithubDownloadAsset("AU-Avengers", "TOU-Mira", "-steam-itch.zip");
        var filePath = await GithubManager.DownloadFile(baseModUrl, moddedFolder, _progress);
        _progress.FinishStep();
        
        _progress?.NextStep(10);
        await Task.Run(() =>
        {
            ExtractZip(filePath, moddedFolder);
        });
        File.Delete(filePath);
        _progress?.FinishStep();
        
        var pluginFolder = Path.Join(moddedFolder, "BepinEx", "plugins");
        if (!Directory.Exists(pluginFolder))
        {
            Console.Write(pluginFolder + " not found");
            //maybe throw error here instead
            return "";
        }
                
        File.Delete(Path.Join(pluginFolder, "AUnlocker.dll"));
        List<(string repoOwner, string repoName)> plugins = 
        [
            ("SubmergedAmongUs","Submerged"),
            ("DigiWorm0","LevelImposter"),
            ("rewalo","TownOfUsMiraRolesExtension"),
            ("xChipseq","ModExplorer"),
            ("astra1dev", "AUnlocker")
        ];

        var stepPerPlugin = 30 / plugins.Count;
        
        foreach (var plugin in plugins)
        {
            _progress?.NextStep(stepPerPlugin);
            var pluginUrl = await GithubManager.FindLatestGithubDownloadAsset(plugin.repoOwner, plugin.repoName, ".dll");
            await GithubManager.DownloadFile(pluginUrl, pluginFolder, _progress);
            _progress?.FinishStep();
        }
        
        Console.Out.WriteLine("Finished downloads");

        return moddedFolder;
    }

    public static async Task<bool> AddToSteam(string steamPath, string moddedFolder)
    {
        var moddedExeFilePath = Path.Join(moddedFolder, GameName + ".exe");
        
        var currentSteamUserId = SteamManager.FindCurrentSteamUserId(steamPath);
        var iconPath = await GetCustomAssets(steamPath, currentSteamUserId);
        return SteamManager.AddShortcut(steamPath, moddedFolder, moddedExeFilePath, iconPath, currentSteamUserId);
    }

    public static async Task InstallGame(string steamPath)
    {
        await SteamManager.LaunchSteam(steamPath);
        
        Process.Start(new ProcessStartInfo($"steam://install/{SteamGameId}") { UseShellExecute = true });
    }

    public static async Task CleanUpGameFiles(string steamPath, string steamCommon)
    {
        foreach (var dir in Directory.GetDirectories(steamCommon, $"{GameName}*", SearchOption.TopDirectoryOnly))
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
    
    private static async Task<string> GetCustomAssets(string steamPath, long currentSteamUserId)
    {
        var gridFolderPath = Path.Join(steamPath, "userdata", currentSteamUserId.ToString(), "config", "grid");
        if (!Directory.Exists(gridFolderPath)) Directory.CreateDirectory(gridFolderPath);
            
        const string steamGridId = "4294662226"; //only if game id is -305070
        List<(string url, string fileEnding)> assets =
        [
            ("https://cdn2.steamgriddb.com/grid/24330531679f7fd5318e3e9dde4e1c99.png", "p.png"),
            ("https://cdn2.steamgriddb.com/hero/1cc1fab198176208789cf94b71412dc8.png", "_hero.png"),
            ("https://cdn2.steamgriddb.com/logo/1d92bf06b68f0b08837d6d88412df8ec.png", "_logo.png"),
            ("https://cdn2.steamgriddb.com/icon/588bc7654c8815a85a09b0bc6d82a29f.png", "_icon.png")
        ];

        var stepPerAsset = 10 / assets.Count;
        
        const string logoConfig =
            "{\"nVersion\":1,\"logoPosition\":{\"pinnedPosition\":\"CenterCenter\",\"nWidthPct\":23.704171934260415,\"nHeightPct\":65.2777777777778}}"; //make actual json reader/write if i feel like it
        File.WriteAllText(Path.Join(gridFolderPath, $"{steamGridId}.json"), logoConfig);
        
        foreach (var asset in assets)
        {
            _progress?.NextStep(stepPerAsset);
            var grid = await GithubManager.DownloadFile(asset.url, gridFolderPath, _progress);
            var renamedGrid = Path.Join(gridFolderPath, steamGridId + asset.fileEnding);
            File.Move(grid,  renamedGrid, true);
            _progress?.FinishStep();

            if (asset == assets.Last()) return renamedGrid;
        }

        return string.Empty;
    }

    public static async Task RestartSteam(string steamPath)
    {
        if (!SteamManager.IsSteamRunning()) return;
        
        var steamExe = Path.Combine(steamPath, "steam.exe");

        if (!File.Exists(steamExe)) return;

        Process.Start(steamExe, "-shutdown");
        await Task.Delay(3000);
        Process.Start(steamExe);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, ProgressReporter reporter)
    {
        var files = Directory.GetFiles(
            sourceDir,
            "*",
            SearchOption.AllDirectories);

        long totalBytes = files.Sum(f => new FileInfo(f).Length);
        long copiedBytes = 0;

        Directory.CreateDirectory(destinationDir);

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(sourceDir, file);
            string destinationFile = Path.Combine(destinationDir, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

            File.Copy(file, destinationFile, true);

            copiedBytes += new FileInfo(file).Length;

            double percent = copiedBytes * 100.0 / totalBytes;

            reporter.Report(percent, "Mod Install wird erstellt");
        }
    }

    private static void ExtractZip(string zipPath, string extractPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        var roots = archive.Entries
            .Select(e => e.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(x => x != null)
            .Distinct()
            .ToList();

        var singleRootFolder =
            roots.Count == 1 &&
            archive.Entries.All(e => e.FullName.StartsWith(roots[0] + "/"));

        var total = archive.Entries.Count;
        var current = 0;
        
        foreach (var entry in archive.Entries)
        {
            current++;
            
            var relativePath = entry.FullName;

            if (singleRootFolder)
            {
                // Remove the root folder from the path
                relativePath = relativePath[roots[0]!.Length..].TrimStart('/');
            }

            if (string.IsNullOrEmpty(relativePath))
                continue;

            string destinationPath = Path.Combine(extractPath, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (!string.IsNullOrEmpty(entry.Name)) // skip directories
            {
                entry.ExtractToFile(destinationPath, true);
            }
            
            var percent = (double)current / total * 100;
            _progress?.Report(percent, $"Extracting {entry.Name}");
        }
    }
}