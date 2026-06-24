using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace CInstaller;

public static class Updater
{
    private static HttpResponseMessage? ResponseCache;
    
    private static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version!;
    
    public static async Task<bool> CheckForUpdate()
    {
        var currentVersion = GetCurrentVersion();
        ResponseCache = await GithubManager.FindLatestGithubRelease("Kiirb", "CInstaller");

        var json = await ResponseCache.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var latestVersionString = doc.RootElement[0].GetProperty("tag_name").ToString();

        Version.TryParse(latestVersionString, out Version? latestVersion);
        
        return latestVersion > currentVersion;
    }

    public static async Task RunUpdate(ProgressReporter reporter)
    {
        var url = await GithubManager.FindLatestGithubDownloadAsset("Kiirb", "CInstaller", ".exe", ResponseCache);
        
        reporter.NextStep(100);
        var newFile = await GithubManager.DownloadFile(url, Directory.GetCurrentDirectory(), reporter);
        reporter.FinishStep();

        var currentFile = Environment.ProcessPath;
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c timeout 2 && del {currentFile}",
            CreateNoWindow = true,
            UseShellExecute = false
        });
        
        Process.Start(newFile);
    }
}