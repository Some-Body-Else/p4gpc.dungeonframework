using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonloader.JsonClasses
{
    
    public class DungeonFloor
    {
        public UInt16 ID { get; set; }
        public UInt16 subID { get; set; }
        public UInt32 Byte04 { get; set; }
        public byte tileCountMin { get; set; }
        public byte tileCountMax { get; set; }
        public UInt16 Byte0A { get; set; }
        public byte dungeonScript{ get; set; }
        public byte usedEnv{ get; set; }
        public string? floorName { get; set; }
        public nuint nameAddress { get; set; }
    }
}
