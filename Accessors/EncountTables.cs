using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;
using Reloaded.Memory;
using Reloaded.Memory.Sigscan;
using Reloaded.Mod.Interfaces;

using static Reloaded.Hooks.Definitions.X64.FunctionAttribute;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Diagnostics;

using p4gpc.dungeonframework.Exceptions;
using p4gpc.dungeonframework.JsonClasses;
using p4gpc.dungeonframework.Configuration;
using System.Reflection;
using Reloaded.Memory.Pointers;
using static p4gpc.dungeonframework.Configuration.Config;
using static p4gpc.dungeonframework.JsonClasses.FloorEncounter;

namespace p4gpc.dungeonframework.Accessors
{
    public class EncountTables : Accessor
    {
        // Typically like to keep these private to prevent too much crossover, but my hands are tied here.
        // Look at ReplaceStartupSearch function in MinimapTable for explanation.
        public static nuint _enemyEncountersAddress;
        public static nuint _floorEncountersAddress;
        public static nuint _lootTablesAddress;

        private List<EnemyEncounter> _enemyEncounters;
        private List<FloorEncounter> _floorEncounters;
        private List<LootTable> _lootTables;

        public EncountTables(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _enemyEncounters = jsonImporter.GetEnemyEncounters();
            _floorEncounters = jsonImporter.GetEncounterTables();
            _lootTables = jsonImporter.GetLootTables();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("ENCOUNT.TBL addresses established.", DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {
            _enemyEncountersAddress = _memory.Allocate(_enemyEncounters.Count()*0x16);
            _utils.LogDebug($"New enemy encounter table address: {_enemyEncountersAddress.ToString("X8")}", Config.DebugLevels.TableLocations);
            _utils.LogDebug($"New enemy encounter table size: {(_enemyEncounters.Count()*0x16).ToString("X8")} bytes", Config.DebugLevels.TableLocations);

            _floorEncountersAddress = _memory.Allocate(_floorEncounters.Count()*0xFC);
            _utils.LogDebug($"New floor encounter table address: {_floorEncountersAddress.ToString("X8")}", Config.DebugLevels.TableLocations);
            _utils.LogDebug($"New floor encounter table size: {(_floorEncounters.Count()*0xFC).ToString("X8")} bytes", Config.DebugLevels.TableLocations);

            _lootTablesAddress = _memory.Allocate(_lootTables.Count()*0x15C);
            _utils.LogDebug($"New floor loot table address: {_lootTablesAddress.ToString("X8")}", Config.DebugLevels.TableLocations);
            _utils.LogDebug($"New floor loot table size: {(_lootTables.Count()*0x15C).ToString("X8")} bytes", Config.DebugLevels.TableLocations);

            int counter = 0;
            foreach (EnemyEncounter encounter in _enemyEncounters)
            {
                _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), encounter.Flags);
                counter += 4;
                _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), encounter.Field04);
                counter += 2;
                _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), encounter.Field06);
                counter += 2;
                foreach (UInt16 unit in encounter.Units)
                {
                    _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), unit);
                    counter += 2;
                }
                _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), encounter.FieldID);
                counter += 2;
                _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), encounter.RoomID);
                counter += 2;
                _memory.SafeWrite(_enemyEncountersAddress + (nuint)(counter), encounter.MusicID);
                counter += 2;
            }

            counter = 0;
            foreach (FloorEncounter table in _floorEncounters)
            {
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.NormalWeightRegular);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.NormalWeightRain);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.AlwaysFF);
                counter += 1;

                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.RareWeightRegular);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.RareWeightRain);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.PercentRare);
                counter += 1;

                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.GoldWeightRegular);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.GoldWeightRain);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), table.PercentGold);
                counter += 1;

                // Three more bytes follow, appearing to always be 0
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), (byte)0);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), (byte)0);
                counter += 1;
                _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), (byte)0);
                counter += 1;
                
                foreach (var encounter in table.RegularEncountersNormal)
                {
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[0]);
                    counter += 2;
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[1]);
                    counter += 2;
                }
                foreach (var encounter in table.RegularEncountersRare)
                {
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[0]);
                    counter += 2;
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[1]);
                    counter += 2;
                }
                foreach (var encounter in table.RegularEncountersGold)
                {
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[0]);
                    counter += 2;
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[1]);
                    counter += 2;
                }


                foreach (var encounter in table.RainyEncountersNormal)
                {
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[0]);
                    counter += 2;
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[1]);
                    counter += 2;
                }
                foreach (var encounter in table.RainyEncountersRare)
                {
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[0]);
                    counter += 2;
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[1]);
                    counter += 2;
                }
                foreach (var encounter in table.RainyEncountersGold)
                {
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[0]);
                    counter += 2;
                    _memory.SafeWrite(_floorEncountersAddress + (nuint)(counter), encounter[1]);
                    counter += 2;
                }
            }

            counter = 0;
            foreach (LootTable table in _lootTables)
            {
                foreach (var entry in table.LootEntries)
                {
                    _memory.SafeWrite(_lootTablesAddress + (nuint)(counter), entry.ItemWeight);
                    counter += 2;
                    _memory.SafeWrite(_lootTablesAddress + (nuint)(counter), entry.ItemID);
                    counter += 2;
                    _memory.SafeWrite(_lootTablesAddress + (nuint)(counter), entry.ChestFlags);
                    counter += 2;
                    if (entry.ItemID != 0)
                    {
                        _memory.SafeWrite(_lootTablesAddress + (nuint)(counter), (byte)1);
                    }
                    else
                    {
                        _memory.SafeWrite(_lootTablesAddress  + (nuint)(counter), (byte)0);
                    }
                    counter += 1;
                    _memory.SafeWrite(_lootTablesAddress + (nuint)(counter), entry.ChestModel);
                    counter += 1;
                    _memory.SafeWrite(_lootTablesAddress + (nuint)(counter), (uint)0);
                    counter += 4;
                }
            }
            
        }
    }
}
