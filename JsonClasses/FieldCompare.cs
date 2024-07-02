using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace p4gpc.dungeonframework.JsonClasses
{
    public enum RoomLoadType
    {
        STATIC,
        NONSTATIC,
        PREGENERATED,
        BATTLE
    }
    public class FieldCompare
    {
        public List<RoomLoadType> fieldArray { get; set; }
    }
}
