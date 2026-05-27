using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace CInstaller;

public static class SteamShortcutManager
{
    private const long SteamId64Base = 76561197960265728L;

    public static string FindSteamPathFromGame(string gamePath)
    {
        var steamFolder = Directory.GetParent(gamePath)?.Parent?.Parent;

        if (steamFolder != null && steamFolder.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        {
            return steamFolder.FullName;
        }
        
        Console.WriteLine($"SteamShortcutManager: findSteamPath: {steamFolder}");
        return null!;
    }

    public static long FindCurrentSteamUserId(string steamPath)
    {
        var loginVdfFile = Path.Combine(steamPath, "config", "loginusers.vdf");

        if (!File.Exists(loginVdfFile))
            throw new ArgumentException("SteamShortcutManager: login users.vdf not found");

        var content = File.ReadAllText(loginVdfFile);

        var userBlock = new Regex(
            "\"(\\d{17})\"\\s*\\{[^}]*?\"MostRecent\"\\s*\"1\"",
            RegexOptions.Singleline
        );

        var match = userBlock.Match(content);

        if (!match.Success)
            throw new Exception("No logged-in Steam user found");

        var steamId64 = long.Parse(match.Groups[1].Value);

        return steamId64 - SteamId64Base;
    }

    private static Dictionary<string, object> ParseShortcuts(string path)
    {
        if (!File.Exists(path))
            return new() { ["shortcuts"] = new Dictionary<string, object>() };

        using var stream = File.OpenRead(path);
        return new BinVdfReader(stream).ReadDict();
    }

    private static void SaveShortcuts(string path, Dictionary<string, object> map)
    {
        using var stream = File.Open(path, FileMode.Create);
        new BinVdfWriter(stream).WriteDict(map);
    }

    private static void ClearVdfFile(Dictionary<string, object> rootMap)
    {
        var shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];
        shortcutMap.Clear();
    }

    private static void AddNewGameMap(Dictionary<string, object> rootMap, string newGameFolderPath, string newGameExePath, string icon)
    {
        var shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];

        var nextIndex = shortcutMap.Count;
        var key = nextIndex.ToString();

        var newGameShortcut = new Dictionary<string, object>();

        newGameShortcut["appid"] = -305070;
        newGameShortcut["AppName"] = Path.GetFileName(newGameFolderPath);
        newGameShortcut["Exe"] = newGameExePath;
        newGameShortcut["StartDir"] = newGameFolderPath;
        newGameShortcut["icon"] = icon;
        newGameShortcut["ShortcutPath"] = "";
        newGameShortcut["LaunchOptions"] = "";
        newGameShortcut["IsHidden"] = 0;
        newGameShortcut["AllowDesktopConfig"] = 1;
        newGameShortcut["AllowOverlay"] = 1;
        newGameShortcut["OpenVR"] = 0;
        newGameShortcut["Devkit"] = 0;
        newGameShortcut["DevkitGameID"] = "";
        newGameShortcut["DevkitOverrideAppID"] = 0;
        newGameShortcut["LastPlayTime"] = 0;
        newGameShortcut["FlatpakAppID"] = "";
        newGameShortcut["sortas"] = "";
        newGameShortcut["tags"] = new Dictionary<string, object>();

        shortcutMap[key] = newGameShortcut;
    }

    public static bool IsSteamRunning()
    {
        return Process.GetProcessesByName("steam").Length > 0;
    }

    private static bool ShortcutExists(Dictionary<string, object> rootMap, string newGameFolderPath, string newGameExePath)
    {
        var shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];

        foreach (var value in shortcutMap.Values)
        {
            var game = (Dictionary<string, object>)value;
            
            var exeRaw = game.ContainsKey("Exe") ? game["Exe"].ToString() : null;
            var dirRaw = game.ContainsKey("StartDir") ? game["StartDir"].ToString() : null;

            if (exeRaw == null || dirRaw == null)
                continue;

            exeRaw = exeRaw.Replace("\"", "");

            if (exeRaw.Equals(newGameExePath, StringComparison.OrdinalIgnoreCase) ||
                dirRaw.Equals(newGameFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
    
    public static void LaunchSteam(string steamPath)
    {
        var steamExe = Path.Join(steamPath, "steam.exe");
        if  (!File.Exists(steamExe)) return;

        if (IsSteamRunning())
        {
            Process.Start(new ProcessStartInfo($"{steamExe} -silent") { UseShellExecute = true });//TODO maybe remove on final build

            while (IsSteamRunning())
            {
                Thread.Sleep(3000);
            }
        }
        
        Thread.Sleep(3000);
    }


    public static bool AddShortcut(string steamPath, string newGameFolderPath, string newGameExePath, string icon, long currentSteamUserId)
    {
        var shortcutVdfFile = Path.Combine(steamPath, "userdata", currentSteamUserId.ToString(), "config", "shortcuts.vdf");
        
        var rootMap = ParseShortcuts(shortcutVdfFile);
        
        if (!ShortcutExists(rootMap, newGameFolderPath, newGameExePath))
        {
            AddNewGameMap(rootMap, newGameFolderPath, newGameExePath, icon);

            SaveShortcuts(shortcutVdfFile, rootMap);

            return true;
        }

        return false;
    }
}