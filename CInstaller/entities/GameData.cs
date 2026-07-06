using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CInstaller;

public class GameData
{
    public string Name { get; set; } = "";
    public string CoverFile { get; set; } = "";
    public string InstallDirectory { get; set; } = "";
    public string DownloadLink { get; set; } = "";
    
    public int SteamId { get; set; } = 0;
}