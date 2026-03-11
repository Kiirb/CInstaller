using Microsoft.VisualBasic;
using Microsoft.Win32;
using System.Text.Json;

Main();
return;

void Main()
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

void CopyDirectory(string sourceDir, string destinationDir)
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

void DownloadBase(string dest, string repoOwner, string repoName, string pattern)
{
    string url = FindLatestGithubDownloadAsset(repoOwner, repoName, pattern);
    DownloadFile(url, dest);
}

string FindLatestGithubDownloadAsset(string repoOwner, string repoName, string searchPattern)
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


void DownloadFile(string url, string outputPath)
{
    using var client = new HttpClient();

    var response = client.GetAsync(url).Result;
    response.EnsureSuccessStatusCode();

    using var stream = response.Content.ReadAsStreamAsync().Result;
    string outputFile = Path.Join(outputPath, Path.GetFileName(url));
    using var file = File.Create(outputFile);

    stream.CopyTo(file);
}