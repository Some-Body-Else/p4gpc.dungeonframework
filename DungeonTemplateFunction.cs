using System;
using System.Collections.Generic;
using System.Linq;

using p4gpc.dungeonloader.JsonClasses;

using Reloaded.Hooks.Definitions.X86;
using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;


//Currently vestigial, pending for deletion
namespace p4gpc.dungeonloader
{
    public class DungeonTemplateFunction
    {
        private List<DungeonTemplates> _dungeonTemplates;

        public DungeonTemplateFunction(List<DungeonTemplates> dungeonTemplates)
        {
            _dungeonTemplates = dungeonTemplates;
        }

        public int getRoomCount(int templateIndex)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            templateIndex /= 3;
            return _dungeonTemplates.ElementAt(templateIndex).roomCount;
        }

        public int getRoomExCount(int templateIndex)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            //We will presumably change this once we have the foundations for this mod a bit more settled
            templateIndex /= 3;
            return _dungeonTemplates.ElementAt(templateIndex).roomExCount;
        }

        public int getRoom(int templateIndex, int roomIndex)
        {
            if (roomIndex >= _dungeonTemplates.ElementAt(templateIndex).rooms.Count || roomIndex <= 0)
            {
                throw new ArgumentOutOfRangeException("roomIndex", $"Attempting to access room index {roomIndex} while there exist {_dungeonTemplates.ElementAt(templateIndex).rooms.Count} rooms.");
            }
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            templateIndex /=  3;
            //Room Index is the same value, however
            return _dungeonTemplates.ElementAt(templateIndex).rooms[roomIndex];
        }

    }
}
