using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    public class FloorEncounter
    {
        public byte NormalWeightRegular { get; set; }
        public byte NormalWeightRain { get; set; }
        public byte AlwaysFF { get; set; }
        public byte RareWeightRegular { get; set; }
        public byte RareWeightRain { get; set; }
        public byte PercentRare { get; set; }
        public byte GoldWeightRegular { get; set; }
        public byte GoldWeightRain { get; set; }
        public byte PercentGold { get; set; }
        public List<List<UInt16>> RegularEncountersNormal { get; set; }
        public List<List<UInt16>> RegularEncountersRare { get; set; }
        public List<List<UInt16>> RegularEncountersGold { get; set; }
        public List<List<UInt16>> RainyEncountersNormal { get; set; }
        public List<List<UInt16>> RainyEncountersRare { get; set; }
        public List<List<UInt16>> RainyEncountersGold { get; set; }
    }
}
