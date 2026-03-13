using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Win32;

namespace CInstaller;

public static class Installer
{
    private static ProgressReporter? _progress;
    private static readonly HttpClient _httpClient = new();
    
    public static async Task RunInstaller(ProgressReporter progress)
    {
        _progress = progress;
        
        var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString();
        
        if (string.IsNullOrEmpty(steamPath))
        {
            Console.WriteLine("Steam not found");
            return;
        }
        
            /*
        string steamGameId = "945360";
        string gameManifest = Path.Join(steamPath, "steamapps", "appmanifest_" + steamGameId + ".acf");
        */

        const string gameName = "Among us"; //Get from manifest later
        var steamCommon = Path.Join(steamPath, "steamapps", "common");
        var gameFolder = Path.Join(steamCommon, gameName);
        var moddedFolder = Path.Join(steamCommon, gameName + " Modded");
        var moddedExeFilePath = Path.Join(moddedFolder, gameName + ".exe");

        Console.Out.WriteLine(gameFolder);

        if (!Directory.Exists(gameFolder))
        {
            Console.Write(gameFolder + " not found");
            return;
        }
        
        await Task.Run(() => CopyDirectory(gameFolder, moddedFolder));
        
        _progress.NextStep(40);
        var baseModUrl = await FindLatestGithubDownloadAsset("AU-Avengers", "TOU-Mira", "-steam-itch.zip");
        var filePath = await DownloadFile(baseModUrl, moddedFolder);
        _progress.FinishStep();
        
        _progress?.NextStep(20);
        ExtractZip(filePath, moddedFolder);
        File.Delete(filePath);
        _progress?.FinishStep();
        
        var pluginFolder = Path.Join(moddedFolder, "BepinEx", "plugins");
        if (!Directory.Exists(pluginFolder))
        {
            Console.Write(pluginFolder + " not found");
            return;
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
            var pluginUrl = await FindLatestGithubDownloadAsset(plugin.repoOwner, plugin.repoName, ".dll");
            await DownloadFile(pluginUrl, pluginFolder);
            _progress?.FinishStep();
        }
        
        if (!File.Exists(moddedExeFilePath) || string.IsNullOrEmpty(steamPath)) return;
        
        var currentSteamUserId = SteamShortcutManager.FindCurrentSteamUserId(steamPath);
        var iconPath = await GetCustomAssets(steamPath, currentSteamUserId);
        var addedShortcut = SteamShortcutManager.AddShortcut(steamPath, moddedFolder, moddedExeFilePath, iconPath, currentSteamUserId);
        
        if (addedShortcut) RestartSteam(steamPath);

        Console.Out.WriteLine("Done!");
    }

    private static async Task<string> GetCustomAssets(string steamPath, long currentSteamUserId)
    {
        var gridFolderPath = Path.Join(steamPath, "userdata", currentSteamUserId.ToString(), "config", "grid");
        const string steamGridId = "4294662226"; //only if game id is -305070
        List<(string url, string fileEnding)> assets =
        [
            ("https://cdn2.steamgriddb.com/grid/24330531679f7fd5318e3e9dde4e1c99.png", "p.png"),
            ("https://drive.google.com/uc?export=download&id=1zBoSGPPpe-wZW3CxGB-DFCWs6_rqUcY0", "_hero.png"),
            ("https://drive.google.com/uc?export=download&id=186kqfm7WA5jPnSr5A-jiiAjPcxS7GNKl", "_logo.png"),
            ("https://cdn2.steamgriddb.com/icon/588bc7654c8815a85a09b0bc6d82a29f.png", "_icon.png")
        ];

        var stepPerAsset = 10 / assets.Count;

        foreach (var asset in assets)
        {
            _progress?.NextStep(stepPerAsset);
            var grid = await DownloadFile(asset.url, gridFolderPath);
            var renamedGrid = Path.Join(gridFolderPath, steamGridId + asset.fileEnding);
            File.Move(grid,  renamedGrid, true);
            _progress?.FinishStep();

            if (asset == assets.Last()) return grid;
        }
        
        return string.Empty;
    }

    private static void RestartSteam(string steamPath)
    {
        if (!SteamShortcutManager.IsSteamRunning()) return;
        
        var steamExe = Path.Combine(steamPath, "steam.exe");

        if (!File.Exists(steamExe)) return;

        Process.Start(steamExe, "-shutdown");
        Thread.Sleep(3000);
        Process.Start(steamExe);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destDir);
        }
    }

    private static void ExtractZip(string zipPath, string extractPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        int total = archive.Entries.Count;
        int current = 0;

        foreach (var entry in archive.Entries)
        {
            current++;

            var destinationPath = Path.Combine(extractPath, entry.FullName);

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

            if (!string.IsNullOrEmpty(entry.Name))
            {
                entry.ExtractToFile(destinationPath, true);
            }

            double percent = (double)current / total * 100;
            _progress?.Report(percent, $"Extracting {entry.Name}");
        }
    }



    private static async Task<string> FindLatestGithubDownloadAsset(
        string repoOwner,
        string repoName,
        string searchPattern)
    {
        var githubUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases";
        
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CSharpApp");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var response = await _httpClient.GetAsync(githubUrl);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var assets = doc.RootElement[0].GetProperty("assets");

        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            var url = asset.GetProperty("browser_download_url").GetString();

            if (name != null && name.Contains(searchPattern))
                return url!;
        }

        throw new Exception("No matching asset found.");
    }


    private static async Task<string> DownloadFile(string url, string outputPath)
    {
        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        var filename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? Path.GetFileName(url);
        var outputFile = Path.Join(outputPath, filename);

        using var stream = await response.Content.ReadAsStreamAsync();
        using var file = File.Create(outputFile);

        var buffer = new byte[8192];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            totalRead += read;

            if (totalBytes > 0)
            {
                double percent = (double)totalRead / totalBytes * 100;
                _progress?.Report(percent, $"Downloading {filename}");
            }
        }

        return outputFile;
    }
}