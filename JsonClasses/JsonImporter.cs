using p4gpc.dungeonloader.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
//using Newtonsoft.Json;

namespace p4gpc.dungeonloader.JsonClasses
{
    public class JsonImporter
    {
        private List<DungeonTemplates> _templates;
        private List<DungeonFloors> _floors;
        private List<DungeonRooms> _rooms;
        private FieldCompare _fieldCompare;
        private Dictionary<int, int> _dungeon_template_dict = new Dictionary<int, int>();
        private List<String> _templateSearch;
        private List<String> _floorSearch;
        private List<String> _roomSearch;
        private List<String> _fieldCompareSearch;
        private Config _config;
        public JsonImporter(Config config, Utilities _utils)
        {
            //Debugger.Launch();
            _config = config;
            Dictionary<string, int> temp;
            StreamReader jsonReader = new StreamReader(config.Json_Folder_Path + "/dungeon_templates.json");
            string jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _templates = JsonSerializer.Deserialize<List<DungeonTemplates>>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/dungeon_floors.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _floors = JsonSerializer.Deserialize<List<DungeonFloors>>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/dungeon_rooms.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _rooms = JsonSerializer.Deserialize<List<DungeonRooms>>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/field_compares.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _fieldCompare = JsonSerializer.Deserialize<FieldCompare>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/template_search.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            _templateSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/dungeon_template_dict.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            temp = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonContents)!;
            foreach (string key in temp.Keys)
            {
                _dungeon_template_dict.Add(Int32.Parse(key), temp[key]);
            }

            jsonReader = new StreamReader(config.Json_Folder_Path + "/floor_search.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            _floorSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/room_search.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            _roomSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            jsonReader = new StreamReader(config.Json_Folder_Path + "/compare_search.json");
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _fieldCompareSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            jsonReader.Close();
            _utils.Log("JSON files loaded");
        }
        public List<DungeonTemplates> GetTemplates()
        {
            return _templates;
        }

        public List<String> GetTemplateFunctions()
        {
            return _templateSearch;
        }
        public List<DungeonFloors> GetFloors()
        {
            return _floors;
        }

        public List<String> GetFloorFunctions()
        {
            return _floorSearch;
        }
        public List<DungeonRooms> GetRooms()
        {
            return _rooms;
        }

        public List<String> GetRoomFunctions()
        {
            return _roomSearch;
        }

        public FieldCompare GetFieldCompare()
        {
            return _fieldCompare;
        }
        public List<String> GetFieldCompareFunctions()
        {
            return _fieldCompareSearch;
        }

        public Dictionary<int, int> GetDungeonTemplateDictionary()
        {
            return _dungeon_template_dict;
        }
    }
}
