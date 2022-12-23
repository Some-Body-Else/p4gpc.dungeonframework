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

            _floors = _jsonImporter.GetFloors(); ;
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Floor hooks established.");
        }

        protected override void Initialize()
        {
            Debugger.Launch();
            List<long> functions;
            long address;
            int totalTemplateTableSize = 0;


            foreach (DungeonFloor floor in _floors)
            {
                totalTemplateTableSize += 16;
            }


            _newFloorTable = _memory.Allocate(totalTemplateTableSize);
            _utils.LogDebug($"New floor table address: {_newFloorTable.ToString("X8")}", 1);
            _utils.LogDebug($"New floor table size: {_newFloorTable.ToString("X8")} bytes", 1);

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
                address =  ((long)_newFloorTable - function);
                if (address > Int32.MaxValue || Int32.MinValue > address)
                {
                    throw new ToBeNamedException(_utils);
                }
                _memory.SafeWriteRaw((nuint)function+8, BitConverter.GetBytes(address));
            }
            _utils.LogDebug($"First search target replaced", 2);


            functions = _utils.SigScan_FindAll("00 48 8D 05 ?? ?? ?? ?? 48 89 46 30", "FloorTable Access (Wave 2)");
            foreach (long function in functions)
            {
                address =  ((long)_newFloorTable - function);
                if (address > Int32.MaxValue || Int32.MinValue > address)
                {
                    throw new ToBeNamedException(_utils);
                }
                _memory.SafeWriteRaw((nuint)function+4, BitConverter.GetBytes(address));
            }
            _utils.LogDebug($"Second search target replaced", 2);
        }

    }
}
