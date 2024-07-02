using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class DungeonTemplates
    {
        public int roomCount { get; set; }
        public int roomExCount { get; set; }
        public int exitNum { get; set; }
        public List<byte> rooms { get; set; }
    }
}
