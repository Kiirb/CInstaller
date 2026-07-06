using System.IO;
using System.Text.Json;

namespace CInstaller;

public class GameListPoller
{
    private static readonly JsonSerializerOptions Options = new(){WriteIndented = true};
    
    private static void Save<T>(IEnumerable<T> gameList, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(gameList, Options));
    }
    
    private static List<GameData> Load(string path)
    {
        if (!File.Exists(path))
            return new List<GameData>();

        return JsonSerializer.Deserialize<List<GameData>>(
                   File.ReadAllText(path))
               ?? new List<GameData>();
    }
    
    public static void Testing()
    {
        
    }
}