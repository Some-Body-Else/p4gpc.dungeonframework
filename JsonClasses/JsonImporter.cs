using p4gpc.dungeonframework.Configuration;
using p4gpc.dungeonframework.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
//using Newtonsoft.Json;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class JsonImporter
    {
        private List<DungeonTemplates> _templates;
        private List<DungeonFloor> _floors;
        private List<DungeonRoom> _rooms;
        private List<DungeonMinimap> _minimap;
        private List<FieldCompares> _fieldCompares;
        private Dictionary<byte, byte> _dungeon_template_dict = new Dictionary<byte, byte>();
        private List<DungeonLinks> _links;

        private List<EnemyEncounter> _enemyEncounters;
        private List<FloorEncounter> _floorEncounters;
        private List<LootTable> _lootTables;
        private ChestPalette _chestPalettes;


        public JsonImporter(Config config, Utilities _utils, string jsonPath = "", string defaultPath="" )
        {
            Dictionary<string, byte> temp;
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
            _floors = JsonSerializer.Deserialize<List<DungeonFloor>>(jsonContents)!;

            if (File.Exists(jsonPath + "/dungeon_rooms.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_rooms.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_rooms.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_rooms.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_rooms.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _rooms = JsonSerializer.Deserialize<List<DungeonRoom>>(jsonContents)!;

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
            temp = JsonSerializer.Deserialize<Dictionary<string, byte>>(jsonContents)!;
            foreach (string key in temp.Keys)
            {
                _dungeon_template_dict.Add(Byte.Parse(key), temp[key]);
            }


            if (File.Exists(jsonPath + "/encounters.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/encounters.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load encounters.json from Persona 4 Golden mod folder failed, defaulting to vanilla encounters.json");
                }
                jsonReader = new StreamReader(defaultPath + "/encounters.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _enemyEncounters = JsonSerializer.Deserialize<List<EnemyEncounter>>(jsonContents)!;
            
            if (File.Exists(jsonPath + "/encounter_tables.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/encounter_tables.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load encounter_tables.json from Persona 4 Golden mod folder failed, defaulting to vanilla encounter_tables.json");
                }
                jsonReader = new StreamReader(defaultPath + "/encounter_tables.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _floorEncounters = JsonSerializer.Deserialize<List<FloorEncounter>>(jsonContents)!;

            if (File.Exists(jsonPath + "/loot_tables.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/loot_tables.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load loot_tables.json from Persona 4 Golden mod folder failed, defaulting to vanilla loot_tables.json");
                }
                jsonReader = new StreamReader(defaultPath + "/loot_tables.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _lootTables = JsonSerializer.Deserialize<List<LootTable>>(jsonContents)!;


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

            _fieldCompares = new();
            jsonContents = jsonReader.ReadToEnd();
            var tempo = JsonSerializer.Deserialize<List<Dictionary<string, List<byte> > > >(jsonContents)!;
            foreach (var entry in tempo)
            {
                FieldCompares compare = new();
                compare.rooms = new();
                foreach (var key in entry.Keys)
                {
                    RoomEntry roomEntry = new();
                    roomEntry.LoadType = (RoomLoadType)entry[key][0];
                    roomEntry.Flags = entry[key][1];
                    compare.rooms.Add(byte.Parse(key), roomEntry);
                }
                _fieldCompares.Add(compare);

            }

            if (File.Exists(jsonPath + "/dungeon_links.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/dungeon_links.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load dungeon_links.json from Persona 4 Golden mod folder failed, defaulting to vanilla dungeon_links.json");
                }
                jsonReader = new StreamReader(defaultPath + "/dungeon_links.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _links = JsonSerializer.Deserialize<List<DungeonLinks>>(jsonContents)!;


            if (File.Exists(jsonPath + "/chest_palettes.json"))
            {

                jsonReader = new StreamReader(jsonPath + "/chest_palettes.json");
            }
            else
            {
                if (hasCustom && !config.suppressWarnErr)
                {
                    _utils.LogError($"Attempt to load chest_palettes.json from Persona 4 Golden mod folder failed, defaulting to vanilla chest_palettes.json");
                }
                jsonReader = new StreamReader(defaultPath + "/chest_palettes.json");
            }
            jsonContents = jsonReader.ReadToEnd();
            _chestPalettes = JsonSerializer.Deserialize<ChestPalette>(jsonContents)!;


            jsonReader.Close();
        }
        public List<DungeonTemplates> GetTemplates()
        {
            return _templates;
        }
        public List<DungeonFloor> GetFloors()
        {
            return _floors;
        }

        public List<DungeonRoom> GetRooms()
        {
            return _rooms;
        }

        public List<DungeonMinimap> GetMinimap()
        {
            return _minimap;
        }

        public List<FieldCompares> GetFieldCompare()
        {
            return _fieldCompares;
        }

        public Dictionary<byte, byte> GetDungeonTemplateDictionary()
        {
            return _dungeon_template_dict;
        }

        public List<EnemyEncounter> GetEnemyEncounters()
        {
            return _enemyEncounters;
        }

        public List<FloorEncounter> GetEncounterTables()
        {
            return _floorEncounters;
        }

        public List<LootTable> GetLootTables()
        {
            return _lootTables;
        }
        public List<DungeonLinks> GetLinks()
        {
            return _links;
        }

        public ChestPalette GetChestPalettes()
        {
            return _chestPalettes;
        }
    }
}
