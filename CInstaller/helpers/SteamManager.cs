using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace CInstaller.helpers;

public static class SteamManager
{
    private const long SteamId64Base = 76561197960265728L;

    public static string FindSteamPathFromGame(string gamePath)
    {
        DirectoryInfo? steamFolder = Directory.GetParent(gamePath)?.Parent?.Parent;

        if (steamFolder != null && steamFolder.Name.Equals("Steam", StringComparison.OrdinalIgnoreCase))
        {
            return steamFolder.FullName;
        }
        
        Console.WriteLine($"SteamManager: findSteamPath: {steamFolder}");
        return null!;
    }

    public static long FindCurrentSteamUserId(string steamPath)
    {
        string loginVdfFile = Path.Combine(steamPath, "config", "loginusers.vdf");

        if (!File.Exists(loginVdfFile))
            throw new ArgumentException("SteamManager: login users.vdf not found");

        string content = File.ReadAllText(loginVdfFile);

        // Match each "<steamid64>" { ... } block (blocks in this file don't nest braces).
        Regex userBlock = new Regex(
            "\"(\\d{17})\"\\s*\\{(.*?)\\n\\s*\\}",
            RegexOptions.Singleline
        );

        MatchCollection matches = userBlock.Matches(content);

        if (matches.Count == 0)
            throw new Exception("No logged-in Steam user found");

        Match? best = null;
        long bestTimestamp = -1;

        foreach (Match candidate in matches)
        {
            string body = candidate.Groups[2].Value;

            // Prefer the account Steam explicitly marks as the most recent login.
            if (Regex.IsMatch(body, "\"MostRecent\"\\s*\"1\""))
            {
                best = candidate;
                break;
            }
        }

        if (best == null)
        {
            foreach (Match candidate in matches)
            {
                string body = candidate.Groups[2].Value;

                // Fall back to the account Steam will auto-login as.
                if (Regex.IsMatch(body, "\"AutoLogin\"\\s*\"1\""))
                {
                    best = candidate;
                    break;
                }
            }
        }

        if (best == null)
        {
            if (matches.Count == 1)
            {
                best = matches[0];
            }
            else
            {
                // Last resort: pick whichever account logged in most recently by timestamp.
                foreach (Match candidate in matches)
                {
                    Match timestampMatch = Regex.Match(candidate.Groups[2].Value, "\"Timestamp\"\\s*\"(\\d+)\"");
                    if (timestampMatch.Success && long.Parse(timestampMatch.Groups[1].Value) > bestTimestamp)
                    {
                        bestTimestamp = long.Parse(timestampMatch.Groups[1].Value);
                        best = candidate;
                    }
                }
            }
        }

        if (best == null)
            throw new Exception("No logged-in Steam user found");

        long steamId64 = long.Parse(best.Groups[1].Value);

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
        Dictionary<string, object> shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];
        shortcutMap.Clear();
    }

    private static void AddNewGameMap(Dictionary<string, object> rootMap, string newGameFolderPath, string newGameExePath, string icon)
    {
        Dictionary<string, object> shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];

        int nextIndex = shortcutMap.Count;
        string key = nextIndex.ToString();

        Dictionary<string, object> newGameShortcut = new Dictionary<string, object>();

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
        Dictionary<string, object> shortcutMap = (Dictionary<string, object>)rootMap["shortcuts"];

        foreach (object value in shortcutMap.Values)
        {
            Dictionary<string, object> game = (Dictionary<string, object>)value;
            
            string? exeRaw = game.ContainsKey("Exe") ? game["Exe"].ToString() : null;
            string? dirRaw = game.ContainsKey("StartDir") ? game["StartDir"].ToString() : null;

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
    
    public static async Task LaunchSteam(string steamPath)
    {
        var steamExe = Path.Join(steamPath, "steam.exe");
        if  (!File.Exists(steamExe)) return;

        if (!IsSteamRunning())
        {
            Process.Start(new ProcessStartInfo($"{steamExe}" ) { UseShellExecute = true });

            while (!IsSteamRunning())
            {
                await Task.Delay(3000);
            }
        }
    }
    
    public static async Task RestartSteam(string steamPath)
    {
        if (!SteamManager.IsSteamRunning()) return;
        
        var steamExe = Path.Combine(steamPath, "steam.exe");

        if (!File.Exists(steamExe)) return;

        Process.Start(steamExe, "-shutdown");
        await Task.Delay(3000);
        Process.Start(steamExe);
    }

    public static async Task InstallGame(string steamPath, int steamGameId)
    {
        await LaunchSteam(steamPath);
        
        Process.Start(new ProcessStartInfo($"steam://install/{steamGameId}") { UseShellExecute = true });
    }

    public static bool AddShortcut(string steamPath, string newGameFolderPath, string newGameExePath, string icon, long currentSteamUserId)
    {
        var shortcutVdfFile = Path.Combine(steamPath, "userdata", currentSteamUserId.ToString(), "config", "shortcuts.vdf");
        
        var rootMap = ParseShortcuts(shortcutVdfFile);

        if (ShortcutExists(rootMap, newGameFolderPath, newGameExePath)) return false;
        
        AddNewGameMap(rootMap, newGameFolderPath, newGameExePath, icon);

        SaveShortcuts(shortcutVdfFile, rootMap);

        return true;
    }
}