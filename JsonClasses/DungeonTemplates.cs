using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonloader.JsonClasses
{
    public class DungeonTemplates
    {
        public int roomCount { get; set; }
        public int roomExCount { get; set; }
        public List<int> rooms { get; set; }
    }
}
