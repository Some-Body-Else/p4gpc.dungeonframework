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
            -Handle floor names
            --DungeonFloor class stores name under floorName var
            --Also has nameAddress var, but may be vestigial and worth cutting
         */

        private List<DungeonFloor> _floors;
        private nuint _newFloorTable;

        public FloorTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _floors = jsonImporter.GetFloors();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Floor hooks established.");
        }

        protected override void Initialize()
        {
            // Debugger.Launch();
            List<Int64> functions;
            long address;
            int totalTemplateTableSize = 0;


            foreach (DungeonFloor floor in _floors)
            {
                totalTemplateTableSize += 16;
            }


            _newFloorTable = _memory.Allocate(totalTemplateTableSize);
            _utils.LogDebug($"New floor table address: {_newFloorTable.ToString("X8")}", 1);
            _utils.LogDebug($"New floor table size: {totalTemplateTableSize.ToString("X8")} bytes", 1);

            totalTemplateTableSize = 0;
            foreach (DungeonFloor floor in _floors)
            {
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.ID);
                totalTemplateTableSize+=2;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.subID);
                totalTemplateTableSize+=2;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.Byte04);
                totalTemplateTableSize+=4;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.floorMin);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.floorMax);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.ID);
                totalTemplateTableSize+=2;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.dungeonScript);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, floor.usedEnv);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newFloorTable + (nuint)totalTemplateTableSize, 0x00);
                totalTemplateTableSize+=2;
            }
            _utils.LogDebug($"New floor table initialized!");


            functions = _utils.SigScan_FindAll("44 8B 44 24 ?? 48 8D 0D ?? ?? ?? ?? 48 8B D0 E8", "FloorTable Access (Wave 1)");
            foreach (long function in functions)
            {
                byte addValue;
                _memory.SafeRead((nuint)(function + 4), out addValue);
                FloorTableWave1(function, "44 8B 44 24 ?? 48 8D 0D ?? ?? ?? ?? 48 8B D0", addValue);
                _utils.LogDebug($"Replaced Wave1 target at: {function.ToString("X8")}", 5);
                //_memory.SafeWriteRaw((nuint)function+8, BitConverter.GetBytes(address));
            }
            _utils.LogDebug($"First search target replaced", 2);


            // Old search: 81 7E 04 9F 00 00 00 48 8D 05 ?? ?? ?? ?? 48 89 46 30 74 67
            address = _utils.SigScan("81 ?? ?? 9F 00 00 00 ?? ?? 05 ?? ?? ?? ??", "FloorTable Access (Wave 2)");
            FloorTableWave2(address, "81 ?? ?? 9F 00 00 00 ?? ?? 05 ?? ?? ?? ??");
            _utils.LogDebug($"Replaced Wave1 target at: {address.ToString("X8")}", 5);
            _utils.LogDebug($"Second search target replaced", 2);
        }

        private void FloorTableWave1(Int64 functionAddress, string pattern, byte offsetSize)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
            instruction_list.Add($"mov r8d, [rsp + {offsetSize}]");
            instruction_list.Add($"mov rcx, {_newFloorTable}");
            instruction_list.Add($"mov rdx, rax");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void FloorTableWave2(Int64 functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use64");
            instruction_list.Add($"cmp [rsi+4], byte 0x9F");
            instruction_list.Add($"mov rax, {_newFloorTable}");
            /*
             
            instruction_list.Add($"lea eax, [{_newFloorTable}]");
            instruction_list.Add($"mov [rsi+0x30], eax");
            instruction_list.Add($"cmp [rsi+4], 0x0000009F");
            instruction_list.Add($"jne end");
            instruction_list.Add($"push {functionAddress+0x7A}");
            instruction_list.Add($"ret");
            instruction_list.Add($"label end");
             */

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
    }
}
