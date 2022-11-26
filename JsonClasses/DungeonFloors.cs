using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonloader.JsonClasses
{
    
    public class DungeonFloors
    {
        public int ID { get; set; }
        public int subID { get; set; }
        public int Byte04 { get; set; }
        public int floorMin { get; set; }
        public int floorMax { get; set; }
        public int Byte0A { get; set; }
        public int dungeonScript{ get; set; }
        public int usedEnv{ get; set; }
        public string? floorName { get; set; }
        public nuint nameAddress { get; set; }
    }
}
