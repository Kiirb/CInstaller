using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Hashing;

public class SteamShortcutManager
{
    private const long STEAM_ID_64_BASE = 76561197960265728L;

    public static string FindSteamPath(string gamePath)
    {
        var steamFolder = Directory.GetParent(gamePath).Parent.Parent;

        if (steamFolder.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        {
            return steamFolder.FullName;
        }
        else
        {
            Console.WriteLine($"SteamShortcutManager: findSteamPath: {steamFolder}");
            return null;
        }
    }

    public static long FindCurrentSteamUserId(string steamPath)
    {
        string loginVdfFile = Path.Combine(steamPath, "config", "loginusers.vdf");

        if (!File.Exists(loginVdfFile))
            throw new ArgumentException("SteamShortcutManager: loginusers.vdf not found");

        string content = File.ReadAllText(loginVdfFile);

        var userBlock = new Regex(
            "\"(\\d{17})\"\\s*\\{[^}]*?\"MostRecent\"\\s*\"1\"",
            RegexOptions.Singleline
        );

        var match = userBlock.Match(content);

        if (!match.Success)
            throw new Exception("No logged-in Steam user found");

        long steamId64 = long.Parse(match.Groups[1].Value);

        return steamId64 - STEAM_ID_64_BASE;
    }

    public static string FindShortcutsFile(string steamPath, long currentSteamUserId)
    {
        string shortcutFile = Path.Combine(
            steamPath,
            "userdata",
            currentSteamUserId.ToString(),
            "config",
            "shortcuts.vdf"
        );

        if (File.Exists(shortcutFile))
            return shortcutFile;

        throw new ArgumentException("SteamShortcutManager: shortcuts.vdf not found");
    }

    public static Dictionary<string, object> ParseShortcuts(string path)
    {
        using var stream = File.OpenRead(path);
        var reader = new BinVdfReader(stream);
        return reader.ReadDict();
    }

    public static void SaveShortcuts(string path, Dictionary<string, object> map)
    {
        using var stream = File.Open(path, FileMode.Create);
        var writer = new BinVdfWriter(stream);
        writer.WriteDict(map);
    }

    private static void ClearVdfFile(Dictionary<string, object> rootMap)
    {
        var shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];
        shortcutMap.Clear();
    }

    private static void AddNewGameMap(
        Dictionary<string, object> rootMap,
        string newGameFolderPath,
        string newGameExePath,
        string icon)
    {
        var shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];

        int nextIndex = shortcutMap.Count;
        string key = nextIndex.ToString();

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

    public static bool ShortcutExists(Dictionary<string, object> rootMap, string newGameFolderPath, string newGameExePath)
    {
        var shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];

        foreach (var value in shortcutMap.Values)
        {
            var game = (Dictionary<string, object>)value;

            string exeRaw = game.ContainsKey("Exe") ? game["Exe"]?.ToString() : null;
            string dirRaw = game.ContainsKey("StartDir") ? game["StartDir"]?.ToString() : null;

            if (exeRaw == null || dirRaw == null)
                continue;

            exeRaw = exeRaw.Replace("\"", "");

            string existingExe = Path.GetFullPath(exeRaw);
            string existingDir = Path.GetFullPath(dirRaw);

            if (existingExe.Equals(newGameExePath, StringComparison.OrdinalIgnoreCase) ||
                existingDir.Equals(newGameFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool AddShortcut(string steamPath, string newGameFolderPath, string newGameExePath, string icon, long currentSteamUserId)
    {
        string shortcutVdfFile = FindShortcutsFile(steamPath, currentSteamUserId);

        var rootMap = ParseShortcuts(shortcutVdfFile);

        ClearVdfFile(rootMap);
        if (!ShortcutExists(rootMap, newGameFolderPath, newGameExePath))
        {
            AddNewGameMap(rootMap, newGameFolderPath, newGameExePath, icon);

            SaveShortcuts(shortcutVdfFile, rootMap);

            return true;
        }

        return false;
    }
}