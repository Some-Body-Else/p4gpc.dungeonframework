using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class LootTable
    {
        public class LootEntry
        {
            public UInt16 ItemWeight { get; set; }
            public UInt16 ItemID { get; set; }

            // 1 if locked, any other value to indicate opened, but may have more than one purpose
            // *Thinking enemies popping out of chest, need more testing*
            public UInt16 ChestFlags { get; set; }

            // Single byte containing the value 0x01, indicates chest to be valid
            public byte ChestModel { get; set; }
        }
        public List<LootEntry> LootEntries { get; set; }
    }
}
