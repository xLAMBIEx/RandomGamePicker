using RandomGamePicker.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RandomGamePicker.Services
{
    public static class GameStore
    {
        public static string AppDataFolder => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RandomGamePicker");
        public static string StorePath => System.IO.Path.Combine(AppDataFolder, "games.json");


        public static List<GameEntry> Load()
        {
            try
            {
                if (!File.Exists(StorePath)) return new();
                var json = File.ReadAllText(StorePath);
                var data = JsonSerializer.Deserialize<List<GameEntry>>(json) ?? new();
                return data;
            }
            catch { return new(); }
        }


        public static void Save(IEnumerable<GameEntry> entries)
        {
            Directory.CreateDirectory(AppDataFolder);
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StorePath, json);
        }
    }
}
