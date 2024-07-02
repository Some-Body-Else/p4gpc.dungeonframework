using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class DungeonMinimap
    {
        public int roomID { get; set; }
        public bool multipleNames { get; set; }
        public string name { get; set; }
        public List<string> names { get; set; }
        public List<List<float>> texCoordSingle { get; set; }
        public List<List<List<float>>> texCoordMulti { get; set; }
        public List<float> texScaleSingle { get; set; }
        public List<List<float>> texScaleMulti { get; set; }
        public bool singleOrientBased { get; set; }
        public List<bool> multiOrientBased{ get; set; }
    }
}
