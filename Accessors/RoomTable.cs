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
    public class RoomTable : Accessor
    {
        /*
        To do:
            -Account for room connection table (Maybe, since it's tile-by-tile basis, it might have all bases covered already)
            -
         */

        private List<DungeonRoom> _rooms;
        private nuint _newRoomTable;

        public RoomTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _rooms = jsonImporter.GetRooms();
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Room hooks established.");
        }

        protected override void Initialize()
        {
            Debugger.Launch();
            List<long> functions;
            String address_str_old;
            String search_string = "0F ?? ?? ?? ";
            long address;
            uint oldAddress;
            int totalTemplateTableSize = 0;

            List<long> _roomTables = _utils.SigScan_FindAll("48 6B c8 56 0F 10 ?? ??", "RoomTable Reference Function");



            foreach (DungeonRoom room in _rooms)
            {
                totalTemplateTableSize += 86;
            }

            _newRoomTable = _memory.Allocate(totalTemplateTableSize);
            _utils.LogDebug($"New room table address: {_newRoomTable.ToString("X8")}", 1);
            _utils.LogDebug($"New room table size: {_newRoomTable.ToString("X8")} bytes", 1);

            totalTemplateTableSize = 0;
            foreach (DungeonRoom room in _rooms)
            {
                _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize+(nuint)totalTemplateTableSize, room.ID);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, room.sizeX);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, room.sizeY);
                totalTemplateTableSize++;
                foreach (List<byte> connectionRow in room.connectionPointers)
                {
                    foreach (byte connection in connectionRow)
                    {
                        _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, connection);
                        totalTemplateTableSize++;
                    }
                }
                foreach (List<byte> revealRow in room.revealProperties)
                {
                    foreach (byte reveal in revealRow)
                    {
                        _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, reveal);
                        totalTemplateTableSize++;
                    }
                }
                _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, room.unknownMasks[0]);
                totalTemplateTableSize++;
                _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, room.unknownMasks[1]);
                totalTemplateTableSize++;
                foreach (List<byte> row in room.mapRamOutline)
                {
                    foreach (byte value in row)
                    {

                        _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, value);
                        totalTemplateTableSize++;
                    }
                }
                _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, (byte)0);
                totalTemplateTableSize++;
                foreach (List<int> row in room.connectionValues)
                {
                    foreach (int value in row)
                    {
                        _memory.SafeWrite(_newRoomTable+(nuint)totalTemplateTableSize, value);
                        totalTemplateTableSize+=4;
                    }
                }

            }
            _utils.LogDebug($"New room table initialized!");

            // -86 present since room 0 is unused, so need to have the blank space somewhere
            // Not sure if I'm happy with this workaround, but we'll give it a shot

            foreach (long _roomTable in _roomTables)
            { 
                _memory.SafeRead((nuint)_roomTable+9, out oldAddress);

                address_str_old = (oldAddress).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("0F 10 ?? ?? " + address_str_old, $"RoomTable Address Chunk #1 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+4, _newRoomTable - 86);
                }


                address_str_old = (oldAddress+0x10).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("0F 10 ?? ?? " + address_str_old, $"RoomTable Address Chunk #2 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+4, _newRoomTable+0x10 - 86);
                }

                address_str_old = (oldAddress+0x20).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("0F 10 ?? ?? " + address_str_old, $"RoomTable Address Chunk #3 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+4, _newRoomTable+0x20 - 86);
                }

                address_str_old = (oldAddress+0x30).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("0F 10 ?? ?? " + address_str_old, $"RoomTable Address Chunk #4 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+4, _newRoomTable+0x30 - 86);
                }

                address_str_old = (oldAddress+0x40).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("0F 10 ?? ?? " + address_str_old, $"RoomTable Address Chunk #5 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+4, _newRoomTable+0x40 - 86);
                }

                address_str_old = (oldAddress+50).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("8B 84 ?? " + address_str_old, $"RoomTable Address Chunk #6 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+3, _newRoomTable+0x50 - 86);
                }

                address_str_old = (oldAddress+54).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                functions = _utils.SigScan_FindAll("0F B7 ?? ?? " + address_str_old, $"RoomTable Address Chunk #7 [{oldAddress.ToString("X8")}]");
                foreach (long function in functions)
                {
                    _memory.SafeWrite((nuint)function+3, _newRoomTable+0x54 - 86);
                }

            }

            /*             
            0F 10 ?? ?? [ADDRESS]
            8B 84 ?? [ADDRESS]
            0F B7 ?? ?? [ADDRESS]
             */

        }

    }
}
