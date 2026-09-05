using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using CInstaller.entities;
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
        string downloadedFile = await RemoteManager.DownloadFile(url, Path.GetTempPath(), reporter);
        reporter.FinishStep();

        string currentFile = Environment.ProcessPath!;
        string oldFile = currentFile + ".old";

        if (File.Exists(oldFile))
            File.Delete(oldFile);
        
        File.Move(currentFile, oldFile);
        File.Move(downloadedFile, currentFile);
        
        int currentPid = Environment.ProcessId;
        string cleanupScript =
            $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; " +
            $"Remove-Item -LiteralPath '{oldFile}' -Force -ErrorAction SilentlyContinue";

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{cleanupScript}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        Process.Start(currentFile);
    }
}