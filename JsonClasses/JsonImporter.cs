using p4gpc.dungeonloader.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
//using Newtonsoft.Json;

namespace p4gpc.dungeonloader.JsonClasses
{
    public class JsonImporter
    {
        private List<DungeonTemplates> _templates;
        private List<DungeonFloors> _floors;
        private List<DungeonRooms> _rooms;
        private List<DungeonList> _list;
        private Config _config;
        public JsonImporter(Config config, Utilities _utils)
        {
            _config = config;
            StreamReader jsonReader = new StreamReader(config.Json_Folder_Path + "/dungeon_templates.json");
            string jsonContents = jsonReader.ReadToEnd();
            _utils.Log($"\n"+jsonContents);
            _templates = JsonSerializer.Deserialize<List<DungeonTemplates>>(jsonContents)!;
            //Add other json list assignments here
            jsonReader.Close();
        }
        public List<DungeonTemplates> GetTemplates()
        {
            return _templates;
        }
    }
}
