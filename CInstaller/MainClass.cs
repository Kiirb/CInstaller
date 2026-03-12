using System.IO.Compression;
using System.Text.Json;
using Microsoft.Win32;

namespace CInstaller;

public static class MainClass{
    static void Main()
    {
        string steamPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null );

        if (string.IsNullOrEmpty(steamPath))
        {
            Console.WriteLine("Steam not found");
            return;
        }
        
        /*
    string steamGameId = "945360";
    string gameManifest = Path.Join(steamPath, "steamapps", "appmanifest_" + steamGameId + ".acf");
    */

        string gameName = "Among us"; //Get from manifest later
        string steamCommon = Path.Join(steamPath, "steamapps", "common");
        string gameFolder = Path.Join(steamCommon, gameName);
        string moddedFolder = Path.Join(steamCommon, gameName + " Modded");

        Console.Out.WriteLine(gameFolder);
        
        if (!Directory.Exists(gameFolder))
        {
            Console.Write(gameFolder + " not found");
            return;
        }
        
        CopyDirectory(gameFolder, moddedFolder);

        DownloadBase(moddedFolder, "AU-Avengers", "TOU-Mira", "-steam-itch.zip");
    }

    static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (string directory in Directory.GetDirectories(sourceDir))
        {
            string destDir = Path.Combine(destinationDir, Path.GetFileName(directory));
            CopyDirectory(directory, destDir);
        }
    }

    static void DownloadBase(string dest, string repoOwner, string repoName, string pattern)
    {
        string url = FindLatestGithubDownloadAsset(repoOwner, repoName, pattern);
        string filePath = DownloadFile(url, dest);

        if (File.Exists(filePath) && Path.GetExtension(filePath) == ".zip")
        {
            ExtractZip(filePath, dest);
            File.Delete(filePath);
        }
    }

    static void ExtractZip(string zipPath, string extractPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        var roots = archive.Entries
            .Select(e => e.FullName.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(x => x != null)
            .Distinct()
            .ToList();

        bool singleRootFolder =
            roots.Count == 1 &&
            archive.Entries.All(e => e.FullName.StartsWith(roots[0] + "/"));

        foreach (var entry in archive.Entries)
        {
            string relativePath = entry.FullName;

            if (singleRootFolder)
            {
                // Remove the root folder from the path
                relativePath = relativePath.Substring(roots[0].Length).TrimStart('/');
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



    static string FindLatestGithubDownloadAsset(string repoOwner, string repoName, string searchPattern)
    {
        string githubUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/releases/latest";

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CSharpApp");

        var response = client.GetAsync(githubUrl).Result;
        response.EnsureSuccessStatusCode();

        string json = response.Content.ReadAsStringAsync().Result;


        List<(string Name, string Url)> assetsList = new();

        using var doc = JsonDocument.Parse(json);

        JsonElement assets = doc.RootElement.GetProperty("assets");

        foreach (var asset in assets.EnumerateArray())
        {
            string name = asset.GetProperty("name").GetString();
            string url = asset.GetProperty("browser_download_url").GetString();

            assetsList.Add((name, url));
        }
        
        return assetsList.FirstOrDefault(a => a.Name.Contains(searchPattern)).Url;
    }


    static string DownloadFile(string url, string outputPath)
    {
        using var client = new HttpClient();

        var response = client.GetAsync(url).Result;
        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStreamAsync().Result;
        string outputFile = Path.Join(outputPath, Path.GetFileName(url));
        using var file = File.Create(outputFile);

        stream.CopyTo(file);
        
        return outputFile;
    }
}