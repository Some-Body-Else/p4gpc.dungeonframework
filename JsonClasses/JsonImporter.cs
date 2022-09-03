using p4gpc.dungeonloader.Configuration;
using p4gpc.dungeonloader.Exceptions;
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
        public JsonImporter(Config config, Utilities _utils, string jsonPath = "", string defaultPath="" )
        {
            //Debugger.Launch();
            _config = config;
            Dictionary<string, int> temp;
            StreamReader jsonReader;
            bool hasCustom = Directory.Exists(jsonPath);

            if (File.Exists(jsonPath + "/dungeon_templates.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_templates.json");
            }
            else
            {
                if (hasCustom && !config.suppressDefault)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("dungeon_templates.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_templates.json");
            }
            string jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _templates = JsonSerializer.Deserialize<List<DungeonTemplates>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_floors.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_floors.json");
            }
            else
            {

                if (hasCustom && !config.suppressDefault)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("dungeon_floors.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_floors.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _floors = JsonSerializer.Deserialize<List<DungeonFloors>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_rooms.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_rooms.json");
            }
            else
            {
                if (hasCustom && !config.suppressDefault)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("dungeon_rooms.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_rooms.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _rooms = JsonSerializer.Deserialize<List<DungeonRooms>>(jsonContents)!;

            if (File.Exists(jsonPath + "/field_compares.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/field_compares.json");
            }
            else
            {
                if (hasCustom && !config.suppressDefault)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("field_compares.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/field_compares.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _fieldCompare = JsonSerializer.Deserialize<FieldCompare>(jsonContents)!;

            if (File.Exists(jsonPath + "/template_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("template_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default template_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/template_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/template_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressDefault && !config.customSearch)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("template_search.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/template_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            _templateSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/floor_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("floor_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default floor_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/floor_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/floor_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressDefault && !config.customSearch)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("floor_search.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/floor_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            _floorSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/room_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("room_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default room_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/room_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/room_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressDefault && !config.customSearch)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("room_search.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/room_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            _roomSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/compare_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("compare_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default compare_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/compare_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/compare_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressDefault && !config.customSearch) 
                { 
                    _utils.LogError("Warning", new InvalidJsonPathException("compare_search.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/compare_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n"+jsonContents);
            _fieldCompareSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_template_dict.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_template_dict.json");
            }
            else
            {
                if (hasCustom && !config.suppressDefault)
                {
                    _utils.LogError("Warning", new InvalidJsonPathException("dungeon_template_dict.json"));
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_template_dict.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _utils.LogDebug($"\n" + jsonContents);
            temp = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonContents)!;
            foreach (string key in temp.Keys)
            {
                _dungeon_template_dict.Add(Int32.Parse(key), temp[key]);
            }

            jsonReader.Close();
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
