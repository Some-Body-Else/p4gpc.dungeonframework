using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p4gpc.dungeonframework.JsonClasses
{
    // The relations between random dungeon floors, pregenerated floors, and battle field
    // are defined here.
    public class DungeonLinks
    {
        // pregenerated dungeon floors have a different field ID than those of
        // randomly-generated floors, but they pull the assets from the randomly-generated
        // floors, so need to keep track of what is used where
        public List<byte> RandomPregen { get; set; }

        // Fields used for random encounters are picked based on a numerical relation
        // that will be substituted with the information stored here.
        public List<byte> RandomBattle { get; set; }
        
        // Just making sure by also having it for pregen
        public List<byte> PregenBattle { get; set; }

    }
}
