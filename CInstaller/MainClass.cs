using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace CInstaller;

public static class MainClass{
    private static void Main()
    {
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

        CopyDirectory(gameFolder, moddedFolder);

        DownloadBase(moddedFolder, "AU-Avengers", "TOU-Mira", "-steam-itch.zip");
        
        var pluginFolder = Path.Join(moddedFolder, "BepinEx", "plugins");
        if (!Directory.Exists(pluginFolder))
        {
            Console.Write(pluginFolder + " not found");
            return;
        }

        DownloadBase(pluginFolder, "SubmergedAmongUs", "Submerged", ".dll");
        DownloadBase(pluginFolder, "DigiWorm0", "LevelImposter", ".dll");
        DownloadBase(pluginFolder, "rewalo", "TownOfUsMiraRolesExtension", ".dll");
        DownloadBase(pluginFolder, "xChipseq", "ModExplorer", ".dll");

        if (File.Exists(Path.Join(pluginFolder, "AUnlocker.dll"))) DownloadBase(pluginFolder, "astra1dev", "AUnlocker", ".dll");
        if (!File.Exists(moddedExeFilePath) || string.IsNullOrEmpty(steamPath)) return;
        
        var currentSteamUserId = SteamShortcutManager.FindCurrentSteamUserId(steamPath);
        var iconPath = GetCustomAssets(steamPath, currentSteamUserId);
        var addedShortcut = SteamShortcutManager.AddShortcut(steamPath, moddedFolder, moddedExeFilePath, iconPath, currentSteamUserId);
        
        if (addedShortcut) RestartSteam(steamPath);
    }

    private static string GetCustomAssets(string steamPath, long currentSteamUserId)
    {
        var gridFolderPath = Path.Join(steamPath, "userdata", currentSteamUserId.ToString(), "config", "grid");
        const string steamGridId = "4294662226"; //only if game id is -305070
        List<(string url, string fileEnding)> assets =
        [
            ("https://cdn2.steamgriddb.com/grid/24330531679f7fd5318e3e9dde4e1c99.png", "p.png"),
            ("https://drive.google.com/uc?export=download&id=1zBoSGPPpe-wZW3CxGB-DFCWs6_rqUcY0", "_hero.png"),
            ("https://drive.google.com/uc?export=download&id=186kqfm7WA5jPnSr5A-jiiAjPcxS7GNKl", "_logo.png")
        ];

        foreach (var asset in assets)
        {
            var grid = DownloadFile(asset.url, gridFolderPath);
            var renamedGrid = Path.Join(gridFolderPath, steamGridId + asset.fileEnding);
            File.Move(grid,  renamedGrid, true);
        }

        return DownloadFile("https://cdn2.steamgriddb.com/icon/588bc7654c8815a85a09b0bc6d82a29f.png", gridFolderPath); //Icon
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

    private static void DownloadBase(string dest, string repoOwner, string repoName, string pattern)
    {
        var url = FindLatestGithubDownloadAsset(repoOwner, repoName, pattern);
        var filePath = DownloadFile(url, dest);

        if (!File.Exists(filePath) || Path.GetExtension(filePath) != ".zip") return;
        
        ExtractZip(filePath, dest);
        File.Delete(filePath);
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

        foreach (var entry in archive.Entries)
        {
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
        }
    }



    private static string FindLatestGithubDownloadAsset(string repoOwner, string repoName, string searchPattern)
    {
        var githubUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CSharpApp");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

        var response = client.GetAsync(githubUrl).Result;
        response.EnsureSuccessStatusCode();

        var json = response.Content.ReadAsStringAsync().Result;
        
        List<(string Name, string Url)> assetsList = [];

        using var doc = JsonDocument.Parse(json);

        var assets = doc.RootElement[0].GetProperty("assets");

        assetsList.AddRange(from asset in assets.EnumerateArray() let name = asset.GetProperty("name").GetString() let url = asset.GetProperty("browser_download_url").GetString() select (name, url));
        
        return assetsList.FirstOrDefault(a => a.Name.Contains(searchPattern)).Url;
    }


    private static string DownloadFile(string url, string outputPath)
    {
        using var client = new HttpClient();

        var response = client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();
        
        var filename = response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? Path.GetFileName(url);

        using var stream = response.Content.ReadAsStreamAsync().Result;
        var outputFile = Path.Join(outputPath, filename);
        using var file = File.Create(outputFile);

        stream.CopyTo(file);
        
        return outputFile;
    }
}