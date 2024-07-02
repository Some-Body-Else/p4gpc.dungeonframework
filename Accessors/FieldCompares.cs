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

namespace p4gpc.dungeonframework.Accessors
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

        private FieldCompare _fieldCompareTable;
        private nuint _fieldCompareAddress;

        public FieldCompares(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            // _fieldCompareTable = jsonImporter.GetFieldCompare();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Field compare hooks established.", DebugLevels.AlertConnections);
        }

        protected override void Initialize()
        {
            //
            // 
        }

    }
}
