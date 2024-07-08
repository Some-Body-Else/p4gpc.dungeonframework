using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class EnemyEncounter
    {
        public UInt32 Flags { get; set; }
        public UInt16 Field04 { get; set; }
        public UInt16 Field06 { get; set; }
        public List<UInt16>? Units { get; set; }
        public UInt16 FieldID { get; set; }
        public UInt16 RoomID { get; set; }
        public UInt16 MusicID { get; set; }
    }
}
