using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    
    public class DungeonFloor
    {
        public ushort ID { get; set; }
        public ushort subID { get; set; }
        public uint Byte04 { get; set; }
        public byte tileCountMin { get; set; }
        public byte tileCountMax { get; set; }
        public ushort ChestPalette { get; set; }
        public byte dungeonScript { get; set; }
        public byte usedEnv { get; set; }
        public string? floorName { get; set; }
        public ushort EncountTableLookup { get; set; }
        public byte MinEncounterCount { get; set; }
        public byte InitialEncounterCount { get; set; }
        public byte MaxChestCount { get; set; }
        public ushort LootTableLookup { get; set; }
    }
}
