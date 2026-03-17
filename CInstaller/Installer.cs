using System.CodeDom;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CInstaller;

public static partial class Installer
{
    private static ProgressReporter? _progress;
    private static readonly HttpClient HttpClient = new();
    private static string? _steamPath;
    public static bool RestartSteamFlag = false;
    public static bool HardCleanFlag = false;
    private const int SteamGameId = 945360;
    private const string GameName = "Among us";

    public static string FindSteamCommon()
    {
        _steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null)?.ToString();
        
        var steamOrigin = "";
        
        
        var libraryFolderFile = Path.Join(_steamPath, "steamapps", "libraryfolders.vdf");
        var regex = new Regex("\"path\"\\s+\"([^\"]+)\"[\\s\\S]*?\"apps\"\\s*\\{([\\s\\S]*?)\\}", RegexOptions.Multiline);

        foreach (Match lib in regex.Matches(File.ReadAllText(libraryFolderFile)))
        {
            var path = lib.Groups[1].Value.Replace("\\\\", "\\");
            var appsBlock = lib.Groups[2].Value;

            if (Regex.IsMatch(appsBlock, $"\"{SteamGameId}\""))
            {
                steamOrigin = path;
            }
        }
        
        return string.IsNullOrEmpty(steamOrigin) ? "" : Path.Join(steamOrigin, "steamapps", "common");
    }
    
    public static async Task RunInstaller(ProgressReporter progress, string steamCommon)
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CInstaller");
        HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _progress = progress;
        
        var gameFolder = Path.Join(steamCommon, GameName);
        var moddedFolder = Path.Join(steamCommon, GameName + " Modded");
        var moddedExeFilePath = Path.Join(moddedFolder, GameName + ".exe");

        Console.Out.WriteLine(gameFolder);

        if (!Directory.Exists(gameFolder))
        {
            Console.Write(gameFolder + " not found");
            return;
        }

        if (HardCleanFlag) CleanUpGameFiles(steamCommon, SteamGameId);
        
        _progress.Report(0, "Mod Install wird erstellt");
        await Task.Run(() => CopyDirectory(gameFolder, moddedFolder));
        
        _progress.NextStep(50);
        var baseModUrl = await FindLatestGithubDownloadAsset("AU-Avengers", "TOU-Mira", "-steam-itch.zip");
        var filePath = await DownloadFile(baseModUrl, moddedFolder);
        _progress.FinishStep();
        
        _progress?.NextStep(10);
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
        
        if (!File.Exists(moddedExeFilePath) || string.IsNullOrEmpty(_steamPath)) return;
        
        var currentSteamUserId = SteamShortcutManager.FindCurrentSteamUserId(_steamPath);
        var iconPath = await GetCustomAssets(currentSteamUserId);
        RestartSteamFlag = SteamShortcutManager.AddShortcut(_steamPath, moddedFolder, moddedExeFilePath, iconPath, currentSteamUserId);

        Console.Out.WriteLine("Done!");
    }

    private static void CleanUpGameFiles(string steamCommon, int steamGameId)
    {
        _progress?.Report(0, "Wartet auf steam install");
        foreach (var dir in Directory.GetDirectories(steamCommon, $"{GameName}*", SearchOption.TopDirectoryOnly))
        {
            Directory.Delete(dir, true);
        }
        
        Process.Start(new ProcessStartInfo($"steam://validate/{steamGameId}") { UseShellExecute = true });
    
        var gameDownloadFolder = Path.Join(Directory.GetParent(steamCommon)?.ToString(), "downloading", steamGameId.ToString());
        
        Thread.Sleep(3000);
        
        while (Directory.Exists(gameDownloadFolder))
        {
            Thread.Sleep(3000);
        }
        
        Thread.Sleep(1000);
    }

    private static async Task<string> GetCustomAssets(long currentSteamUserId)
    {
        var gridFolderPath = Path.Join(_steamPath, "userdata", currentSteamUserId.ToString(), "config", "grid");
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
            var grid = await DownloadFile(asset.url, gridFolderPath);
            var renamedGrid = Path.Join(gridFolderPath, steamGridId + asset.fileEnding);
            File.Move(grid,  renamedGrid, true);
            _progress?.FinishStep();

            if (asset == assets.Last()) return renamedGrid;
        }

        return string.Empty;
    }

    public static void RestartSteam()
    {
        if (!SteamShortcutManager.IsSteamRunning()) return;

        if (_steamPath != null)
        {
            var steamExe = Path.Combine(_steamPath, "steam.exe");

            if (!File.Exists(steamExe)) return;

            Process.Start(steamExe, "-shutdown");
            Thread.Sleep(3000);
            Process.Start(steamExe);
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Join(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var destDir = Path.Join(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destDir);
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



    private static async Task<string> FindLatestGithubDownloadAsset(
        string repoOwner,
        string repoName,
        string searchPattern)
    {
        var githubUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases";

        var response = await HttpClient.GetAsync(githubUrl);

        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset))
            {
                var resetTime = DateTimeOffset.FromUnixTimeSeconds(long.Parse(reset.First()));
                throw new Exception($"GitHub rate limit exceeded. Try again at {resetTime.LocalDateTime}.");
            }
        }

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
        var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
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