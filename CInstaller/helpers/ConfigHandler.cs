using System;
using System.IO;
using System.Text.Json;
using CInstaller.entities;

namespace CInstaller.helpers;

public static class ConfigHandler
{
    public static string SteamFolder { get; set; } = "";
    public static string SteamCommon { get; set; } = "";
    public static string GameFolder { get; set; } = "";
    public static List<GithubRepo> PluginList { get; set; } = new();

    private const string ConfigFileName = "CInstallerConfig.json";
    public static readonly string ConfigPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, ConfigFileName);

    static ConfigHandler()
    {
        Load();
    }

    private static void Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return;
        }

        string json = File.ReadAllText(ConfigPath);
        ConfigData? loaded = JsonSerializer.Deserialize<ConfigData>(json);

        if (loaded is null) return;

        SteamFolder = loaded.SteamFolder;
        SteamCommon = loaded.SteamCommon;
        GameFolder = loaded.GameFolder;
        PluginList = loaded.PluginList;
    }

    public static void Save(string savePath = "")
    {
        ConfigData data = new()
        {
            SteamFolder = SteamFolder,
            SteamCommon = SteamCommon,
            GameFolder = GameFolder,
            PluginList = PluginList
            
        };

        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(string.IsNullOrEmpty(savePath) ? ConfigPath : Path.Join(savePath, ConfigFileName), json);
    }
    
    private class ConfigData
    {
        public string SteamFolder { get; set; } = "";
        public string SteamCommon { get; set; } = "";
        public string GameFolder { get; set; } = "";
        public List<GithubRepo> PluginList { get; set; } = [];
    }
}
