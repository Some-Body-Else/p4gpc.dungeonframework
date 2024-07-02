using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class DungeonRoom
    {
        public byte ID { get; set; }
        public byte sizeX { get; set; }
        public byte sizeY { get; set; }
        public bool isExit { get; set; }
        public bool hasDoor { get; set; }
        public List<List<byte>> connectionPointers { get; set; }
        public List<List<byte>> revealProperties { get; set; }
        public List<sbyte> x_y_offsets { get; set; }
        public List<List<byte>> mapRamOutline { get; set; }
        public List<List<int>> connectionValues { get; set; }
    }
}
