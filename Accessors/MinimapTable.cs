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

        public MinimapTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {

            _minimaps = _jsonImporter.GetMinimap(); ;
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Minimap hooks established.");
        }

        protected override void Initialize()
        {


        }

    }
}
