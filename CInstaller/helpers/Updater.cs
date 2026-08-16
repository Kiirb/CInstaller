using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using CInstaller.entities;
using static System.Version;

namespace CInstaller.helpers;

public static class Updater
{
    private static HttpResponseMessage? ResponseCache;
    
    public static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version!;
    
    public static async Task<Version?> GetLatestVersion()
    {
        ResponseCache = await RemoteManager.FindLatestGithubRelease("Kiirb", Assembly.GetExecutingAssembly().GetName().Name);
        
        return await RemoteManager.ExtractVersionFromResponse(ResponseCache);
    }

    public static async Task RunUpdate(ProgressReporter reporter)
    {
        string url = await RemoteManager.FindLatestGithubDownloadAsset("Kiirb", Assembly.GetExecutingAssembly().GetName().Name, ".exe", ResponseCache);

        reporter.NextStep(100);
        // Download to a scratch folder so it can't collide with the currently-running exe's filename
        string downloadedFile = await RemoteManager.DownloadFile(url, Path.GetTempPath(), reporter);
        reporter.FinishStep();

        string currentFile = Environment.ProcessPath!;
        string oldFile = currentFile + ".old";

        if (File.Exists(oldFile))
            File.Delete(oldFile);
        
        File.Move(currentFile, oldFile);
        File.Move(downloadedFile, currentFile);
        
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c timeout /t 2 /nobreak >nul && del \"{oldFile}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Process.Start(currentFile);
    }
}