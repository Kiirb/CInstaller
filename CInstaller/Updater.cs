using System.Reflection;

namespace CInstaller;

public class Updater
{
    private static string GithubToken = "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";
    
    private static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version!;
    
    public static async Task<bool> CheckForUpdate()
    {
        var currentVersion = GetCurrentVersion();
        
        //TODO 1. make release 2. get service token for access
        var response = await Installer.FindLatestGithubRelease("Kiirb", "CInsatller", GithubToken);
        
        Console.WriteLine(response.Content);

        return response.Version > currentVersion;
    }

    public static void RunUpdate()
    {
        
    }
}