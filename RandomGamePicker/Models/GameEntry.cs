using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RandomGamePicker.Models
{
    public class GameEntry
    {
        public required string Name { get; set; }
        public required string Path { get; set; } // Full path to .lnk or .exe
        public bool Included { get; set; } = true;


        [JsonIgnore]
        public string FileName => System.IO.Path.GetFileName(Path);
    }
}
