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
        private List<DungeonMinimap> _minimap;
        private FieldCompare _fieldCompare;
        private Dictionary<int, int> _dungeon_template_dict = new Dictionary<int, int>();
        private List<String> _templateSearch;
        private List<String> _floorSearch;
        private List<String> _roomSearch;
        private List<String> _minimapSearch;
        private List<String> _fieldCompareSearch;
        private List<String> _roomCompareSearch;
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
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_templates.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_templates.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_templates.json");
            }
            string jsonContents = jsonReader.ReadToEnd();
            _templates = JsonSerializer.Deserialize<List<DungeonTemplates>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_floors.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_floors.json");
            }
            else
            {

                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_floors.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_floors.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_floors.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _floors = JsonSerializer.Deserialize<List<DungeonFloors>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_rooms.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_rooms.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_minimap.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_rooms.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_rooms.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _rooms = JsonSerializer.Deserialize<List<DungeonRooms>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_minimap.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_minimap.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_minimap.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_minimap.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_minimap.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _minimap = JsonSerializer.Deserialize<List<DungeonMinimap>>(jsonContents)!;

            if (File.Exists(jsonPath + "/field_compares.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/field_compares.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load field_compares.json from Persona 4 Golden mod folder failed, defaulting to vanilla field_compares.json");
                }
                jsonReader = new StreamReader(defaultPath + "/field_compares.json");
            }
            jsonContents = jsonReader.ReadToEnd();
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
                if (hasCustom && !config.suppressWarnErr && !config.customSearch)
                {
                    _utils.LogError($"Attempt to load template_search.json from Persona 4 Golden mod folder failed, defaulting to vanilla template_search.json");
                }
                jsonReader = new StreamReader(defaultPath + "/template_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
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
                if (hasCustom && !config.suppressWarnErr && !config.customSearch)
                {
                    _utils.LogError($"Attempt to load floor_search.json from Persona 4 Golden mod folder failed, defaulting to vanilla floor_search.json");
                }
                jsonReader = new StreamReader(defaultPath + "/floor_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _floorSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/minimap_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("minimap_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default room_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/minimap_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/minimap_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr && !config.customSearch)
                {
                    _utils.LogError($"Attempt to load minimap_search.json from Persona 4 Golden mod folder failed, defaulting to vanilla minimap_search.json");
                }
                jsonReader = new StreamReader(defaultPath + "/minimap_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _minimapSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

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
                if (hasCustom && !config.suppressWarnErr && !config.customSearch)
                {
                    _utils.LogError($"Attempt to load room_search.json from Persona 4 Golden mod folder failed, defaulting to vanilla room_search.json");
                }
                jsonReader = new StreamReader(defaultPath + "/room_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _roomSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/field_compare_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("field_compare_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default field_compare_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/field_compare_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/field_compare_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr && !config.customSearch)
                {
                    _utils.LogError($"Attempt to load field_compare_search.json from Persona 4 Golden mod folder failed, defaulting to vanilla field_compare_search.json");
                }
                jsonReader = new StreamReader(defaultPath + "/field_compare_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _fieldCompareSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;


            if (File.Exists(jsonPath + "/room_compare_search.json"))
            {
                if (!config.customSearch)
                {
                    _utils.LogWarning("room_compare_search.json is present in mod's JSON folder, but custom search is not enabled. Loading default room_compare_search.json.");
                    jsonReader = new StreamReader(defaultPath + "/room_compare_search.json");

                }
                else
                {
                    jsonReader = new StreamReader(jsonPath + "/room_compare_search.json");
                }
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr && !config.customSearch)
                {
                    _utils.LogError($"Attempt to load room_compare_search.json from Persona 4 Golden mod folder failed, defaulting to vanilla room_compare_search.json");
                }
                jsonReader = new StreamReader(defaultPath + "/room_compare_search.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _roomCompareSearch = JsonSerializer.Deserialize<List<String>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_template_dict.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_template_dict.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_template_dict.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_template_dict.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_template_dict.json");
            }
            jsonContents = jsonReader.ReadToEnd();
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

        public List<DungeonMinimap> GetMinimap()
        {
            return _minimap;
        }

        public List<String> GetMinimapFunctions()
        {
            return _minimapSearch;
        }

        public FieldCompare GetFieldCompare()
        {
            return _fieldCompare;
        }
        public List<String> GetFieldCompareFunctions()
        {
            return _fieldCompareSearch;
        }

        public List<String> GetRoomCompareFunctions()
        {
            return _roomCompareSearch;
        }

        public Dictionary<int, int> GetDungeonTemplateDictionary()
        {
            return _dungeon_template_dict;
        }
    }
}
