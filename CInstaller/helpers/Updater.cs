using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using CInstaller.entities;
using CInstaller.helpers;

namespace CInstaller;

public static class Updater
{
    private static HttpResponseMessage? ResponseCache;
    
    public static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version!;
    
    public static async Task<Version?> GetLatestVersion()
    {
        ResponseCache = await RemoteManager.FindLatestGithubRelease("Kiirb", "CInstaller");

        var json = await ResponseCache.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var latestVersionString = doc.RootElement[0].GetProperty("tag_name").ToString();

        Version.TryParse(latestVersionString, out var latestVersion);

        return latestVersion;
    }

    public static async Task RunUpdate(ProgressReporter reporter)
    {
        var url = await RemoteManager.FindLatestGithubDownloadAsset("Kiirb", "CInstaller", ".exe", ResponseCache);
        
        reporter.NextStep(100);
        var newFile = await RemoteManager.DownloadFile(url, Directory.GetCurrentDirectory(), reporter);
        reporter.FinishStep();

        var currentFile = Environment.ProcessPath;
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c timeout /t 2 && del \"{currentFile}\"",
            CreateNoWindow =  true,
            UseShellExecute = false
        });
        
        Process.Start(newFile);
    }
}