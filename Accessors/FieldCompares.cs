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

namespace p4gpc.dungeonloader.Accessors
{
    public class FieldCompares : Accessor
    {
        /*
        To do:
            -Figure out how many permutations there are for these comparisons
            **About 4 major permutations, with ~7 opcodes that lack the patterns
            -Figure out how to handle each permutations

            Coming back to this one later, figure that handling rooms is more important than fields at the moment
         */

        private List<DungeonRoom> _rooms;
        private nuint _newRoomTable;

        public FieldCompares(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {

            _rooms = _jsonImporter.GetRooms(); ;
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Room hooks established.");
        }

        protected override void Initialize()
        {
            

        }

    }
}
