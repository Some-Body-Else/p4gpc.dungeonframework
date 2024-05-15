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

namespace p4gpc.dungeonloader.Accessors
{
    public class FloorTable : Accessor
    {
        /*
        To do:
            Nothing, for now. Something will probably pop up at some point.
            --Clarifying some of
         */

        private List<DungeonFloor> _floors;
        private nuint _newFloorTable;
        private nuint _newFloorNames;
        private nuint _newFloorNamesLookup;

        public FloorTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _floors = jsonImporter.GetFloors();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Floor hooks established.", Config.DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {
            // Debugger.Launch();
            List<Int64> functions;
            List<Int64> nameOffsets = new List<Int64>();
            Int64 offset = 0;
            long address;
            int totalTemplateTableSize = 0;
            int floorNameByteCount = 0;


            foreach (DungeonFloor floor in _floors)
            {
                if (floor.floorName != null)
                {
                    floorNameByteCount+=floor.floorName.Length+1;
                }
            }


            _newFloorTable = _memory.Allocate(_floors.Count()*16);
            _utils.LogDebug($"New floor table address: {_newFloorTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            _utils.LogDebug($"New floor table size: {totalTemplateTableSize.ToString("X8")} bytes", Config.DebugLevels.TableLocations);


            _newFloorNames = _memory.Allocate(floorNameByteCount);
            _utils.LogDebug($"New floor name table address: {_newFloorTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            _utils.LogDebug($"New floor name table size: {totalTemplateTableSize.ToString("X8")} bytes", Config.DebugLevels.TableLocations);

            _newFloorNamesLookup = _memory.Allocate(_floors.Count()*8);
            _utils.LogDebug($"New floor name lookup table address: {_newFloorTable.ToString("X8")}", Config.DebugLevels.TableLocations);
            _utils.LogDebug($"New floor name lookup table size: {totalTemplateTableSize.ToString("X8")} bytes", Config.DebugLevels.TableLocations);

            totalTemplateTableSize = 0;
            int floorNameCounter = 0;
            foreach (DungeonFloor floor in _floors)
            {
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.ID);
                totalTemplateTableSize+=2;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.subID);
                totalTemplateTableSize+=2;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.Byte04);
                totalTemplateTableSize+=4;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.tileCountMin);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.tileCountMax);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.ID);
                totalTemplateTableSize+=2;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.dungeonScript);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.usedEnv);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, 0x00);
                totalTemplateTableSize+=2;

                if (floor.floorName != null)
                {

                    for (int k = 0; k < floor.floorName.Length; k++)
                    {
                        _memory.SafeWrite((_newFloorNames + (nuint)offset + (nuint)k), floor.floorName[k]);
                    }
                    _memory.SafeWrite((_newFloorNames + (nuint)offset + (nuint)floor.floorName.Length), (byte)0);
 
                    _memory.SafeWrite((_newFloorNamesLookup + (nuint)floorNameCounter), (Int64)(_newFloorNames + (nuint)offset));
                    offset += floor.floorName.Length+1;

                }
                else
                {
                    _memory.SafeWrite((_newFloorNamesLookup + (nuint)floorNameCounter), (Int64)(_newFloorNames));
                }
                floorNameCounter+= 8;


            }


            functions = _utils.SigScan_FindAll("44 8B 44 24 ?? 48 8D 0D ?? ?? ?? ?? 48 8B D0 E8", "FloorTable Access (Wave 1)");
            foreach (long function in functions)
            {
                byte addValue;
                _memory.SafeRead((nuint)(function + 4), out addValue);
                FloorTableWave1(function, "44 8B 44 24 ?? 48 8D 0D ?? ?? ?? ?? 48 8B D0", addValue);
                _utils.LogDebug($"Replaced code [44 8B 44 24 ?? 48 8D 0D ?? ?? ?? ?? 48 8B D0] at: {function.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
                //_memory.SafeWriteRaw((nuint)function+8, BitConverter.GetBytes(address));
            }


            // Old search: 81 7E 04 9F 00 00 00 48 8D 05 ?? ?? ?? ?? 48 89 46 30 74 67
            address = _utils.SigScan("81 ?? ?? 9F 00 00 00 ?? ?? 05 ?? ?? ?? ??", "FloorTable Access (Wave 2)");
            FloorTableWave2(address, "81 ?? ?? 9F 00 00 00 ?? ?? 05 ?? ?? ?? ??");
            _utils.LogDebug($"Replaced code [81 ?? ?? 9F 00 00 00 ?? ?? 05 ?? ?? ?? ??] at: {address.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

            // As far as I can tell, field names are handled by a single function that
            // -- Picks an address telling which table to draw the name from
            // -- Pick the name address from the selected table
            // To account for this for now, going to just replace the entry that holds the dungeon floor
            // names. Current address as of 12/20/2023 is 140A801F0
            address = _utils.SigScan("F0 01 A8 40 01 00 00 00", "DungeonFloorNameTableAddress");
            _memory.SafeWrite(address, (Int64)_newFloorNamesLookup);

            _utils.LogDebug($"Replaced address [F0 01 A8 40 01 00 00 00] at: {address.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);

        }

        private void FloorTableWave1(Int64 functionAddress, string pattern, byte offsetSize)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"mov r8d, [rsp + {offsetSize}]");
            instruction_list.Add($"mov rcx, {_newFloorTable}");
            instruction_list.Add($"mov rdx, rax");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void FloorTableWave2(Int64 functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");

            instruction_list.Add($"push rax");
            instruction_list.Add($"push rbx");
            instruction_list.Add($"mov rax, {functionAddress}");
            instruction_list.Add($"mov rbx, {_lastUsedAddress}");
            instruction_list.Add($"mov [rbx], rax");
            instruction_list.Add($"pop rbx");
            instruction_list.Add($"pop rax");

            instruction_list.Add($"cmp [rsi+4], byte 0x9F");
            instruction_list.Add($"mov rax, {_newFloorTable}");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

    }
}
