using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace p4gpc.dungeonframework.JsonClasses
{
    public enum RoomLoadType
    {
        MAP,                // Overworld map
        OVERWORLD,          // Regular fields, think Inaba's shopping district
                                // Also used for shops
        DUNGEON_STATIC,     // Static floors used in dungeon contexts, think Studio Hub
                            // gonna need flags to indicate which battle model is used
        DUNGEON_RANDOM,     // Average dungeon floors
        DUNGEON_PREGEN,     // Dungeon floors with minibosses
        BATTLE              // Punchups
    }
    public class RoomEntry
    {
        public RoomLoadType LoadType { get; set; }
        public byte Flags { get; set; }
    }
    public class FieldCompares
    {
        public Dictionary<byte, RoomEntry> rooms { get; set; }
    }
}
