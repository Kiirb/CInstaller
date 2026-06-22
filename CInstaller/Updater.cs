using System.Reflection;

namespace CInstaller;

public class Updater
{
    private static Version GetCurrentVersion() => Assembly.GetExecutingAssembly().GetName().Version!;
    
    public static async Task<bool> CheckForUpdate()
    {
        var currentVersion = GetCurrentVersion();
        
        //TODO 1. make release 2. get service token for access
        var response = await Installer.FindLatestGithubRelease("Kiirb", "CInsatller");

        return response.Version > currentVersion;
    }

    public static void RunUpdate()
    {
        
    }
}