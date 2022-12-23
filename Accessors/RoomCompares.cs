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
    public class RoomCompares : Accessor
    {
        /*
        To do:
            -Find comparisons
            -Figure out what each comparison checks for
            -Write appropriate functions to replace each comparison
            -Replace comparisons

            -Note comparisons that might need expansion later, like the ones for minimap tile scaling
         */

        private List<DungeonRoom> _rooms;
        private nuint _newRoomTable;

        public RoomCompares(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {

            _rooms = _jsonImporter.GetRooms(); ;
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Room compare hooks established.");
        }

        protected override void Initialize()
        {


        }

    }
}
