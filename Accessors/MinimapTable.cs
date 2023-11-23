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

using p4gpc.dungeonloader.Exceptions;
using p4gpc.dungeonloader.JsonClasses;
using p4gpc.dungeonloader.Configuration;
using System.Reflection;
using Reloaded.Memory.Pointers;
using static System.Formats.Asn1.AsnWriter;
using System.Data.SqlTypes;

namespace p4gpc.dungeonloader.Accessors
{
    public class MinimapTable : Accessor
    {
        /*
        To do:
            -Figure search targets for minimap name load-in
            --Need to update the list of name that load in
            -Figure out what accesses minimap address table Minimap Tile Property Accessor Table
            --Re-allocate to sepearate space to ensure there's enough entries for all new rooms

         */

        private List<DungeonMinimap> _minimaps;
        private nuint _newMinimapTable;
        private nuint _newMinimapLookupTable;
        private nuint _newMinimapNameLookupTable;

        public MinimapTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _minimaps = jsonImporter.GetMinimap();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Minimap hooks established.");
        }

        protected override void Initialize()
        {

            List<long> functions;

            String search_string;
            long address;
            long func;
            uint oldAddress;
            int totalMinimapTableSize = 0;
            byte SIB;
            byte prefixExists;
            AccessorRegister regToZero;

            List<long> _roomTables = new List<long>();
            int minimapCounter = 0;
            int offset = 0;

            /*
             There are two hardcoded tables containing the names of minimap tiles that we need to concern ourselves with.
             One of them only contains the minimap names and is called on startup, used to load in all the minimap tiles into the game.
             Second one has the filepath included with the name, that one is called (as far as I can tell) every frame to render the minimap.

            Both of these tables have a table that actually points to the location of the strings (since their length is variable).
            Majority of all other minimap-related issues involve direct room comparsions, so those are pending to be files under RoomCompares.
             
             Plan for the table is to just have the one table, but have two seperate pointer tables, one pointing to
             the start of the filepath and the other pointing to the start of the filename. That way, we only have 3 tables
             instead of 4 and can perform the same functions. Need to write an algorithm to calculate that out
             
             */

            for (int i = 0; i < _minimaps.Count; i++)
            {
                search_string = "field/smap/";
                search_string += _minimaps[i].name;
                totalMinimapTableSize += search_string.Length;
                offset += search_string.Length;
                minimapCounter++;
                if (_minimaps[i].multipleNames)
                {
                    int numOfVariants = _minimaps[i].names.Count;
                    for (int j = 0; j < numOfVariants; j++)
                    {
                        search_string = "field/smap/";
                        search_string += _minimaps[i].names[j];
                        search_string += char.MinValue;
                        totalMinimapTableSize += search_string.Length;
                        offset += search_string.Length+1;
                        minimapCounter++;
                    }
                }
                else
                {
                }
            }

            _newMinimapTable = _memory.Allocate(totalMinimapTableSize);
            _newMinimapLookupTable = _memory.Allocate(minimapCounter*16);

            offset = 0;
            for (int i = 0; i < _minimaps.Count; i++)
            {

                search_string = "field/smap/";
                search_string += _minimaps[i].name;
                search_string += char.MinValue;
                for (int j = 0; j < search_string.Length; j++)
                {
                    _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)j), search_string[j]);
                }
                _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)search_string.Length), 0);
                _roomTables.Add(offset);
                offset += search_string.Length;
                if (_minimaps[i].multipleNames)
                {
                    int numOfVariants = _minimaps[i].names.Count;
                    for (int j = 0; j < numOfVariants; j++)
                    {
                        search_string = "field/smap/";
                        search_string += _minimaps[i].names[j];
                        search_string += char.MinValue;

                        for (int k = 0; k < search_string.Length; k++)
                        {
                            _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)k), search_string[k]);
                        }
                        _memory.SafeWrite((_newMinimapTable + (nuint)offset + (nuint)search_string.Length), 0);
                        _roomTables.Add(offset);
                        offset += search_string.Length;
                    }
                }
                else
                {
                }
            }

            _newMinimapNameLookupTable = _newMinimapLookupTable + (nuint)minimapCounter*8;

            for (int i = 0; i < minimapCounter; i++)
            {
                _memory.SafeWrite((_newMinimapLookupTable + (nuint)i*8), (ulong)(_newMinimapTable + (nuint)_roomTables[i]) + 11);
                // 11 is length of 'field/smap/', don't think it'll ever change, but noting here in case
                _memory.SafeWrite((_newMinimapNameLookupTable + (nuint)i*8), (ulong)(_newMinimapTable + (nuint)_roomTables[i]));
            }


            // "48 8D 3D ?? ?? ?? ?? 48 8B F3 48 2B F7 48 8D 2D ?? ?? ?? ?? 0F 1F 00"
            // This is the search target for the code that touches the startup table

            func = _utils.SigScan("48 8D 3D ?? ?? ?? ?? 48 8B F3 48 2B F7 48 8D 2D ?? ?? ?? ?? 0F 1F 00", $"StartupMinimapSearch");
            ReplaceStartupSearch(func, 23);
            _utils.LogDebug($"Location: {func.ToString("X8")}", 3);
        }

        void ReplaceStartupSearch(Int64 functionAddress, int length)
        {
            AccessorRegister pushReg;
            List<AccessorRegister> usedRegs;
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
            instruction_list.Add($"mov rdi, {_newMinimapLookupTable}");
            instruction_list.Add($"mov rsi, rbx");
            instruction_list.Add($"sub rsi, rdi");
            instruction_list.Add($"mov rbp, {_newMinimapNameLookupTable}");
            // instruction_list.Add($"");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, length).Activate());
        }

    }
}
