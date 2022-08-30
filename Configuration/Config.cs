using p4gpc.dungeonloader.Configuration.Implementation;
using System.ComponentModel;

namespace p4gpc.dungeonloader.Configuration
{
    public class Config : Configurable<Config>
    {
        /*
        [DisplayName("Default Json Folder Path")]
        [Description("Path that mod expects to find JSON files within, assuming no files are found in the mod directory of P4G.")]
        [DefaultValue("JSON folder within mod's folder")]
        public string defaultPath { get; set; } = (System.IO.Directory.GetCurrentDirectory() + "/Mods/p4gpc.dungeonloader/JSON");
         */


        [DisplayName("Allow custom searches")]
        [Description("Allows DungeonLoader to load in user-created JSON files to find code to replace in the game.\nNOTE: Unless the user intends to replace more of the executable's original code with their own, this should be disabled.")]
        public bool customSearch { get; set; } = false;

        /*
        
        [DisplayName("Int")]
        [Description("This is an int.")]
        [DefaultValue(42)]
        public int Integer { get; set; } = 42;

        [DisplayName("Bool")]
        [Description("This is a bool.")]
        [DefaultValue(true)]
        public bool Boolean { get; set; } = true;

        [DisplayName("Float")]
        [Description("This is a floating point number.")]
        [DefaultValue(6.987654F)]
        public float Float { get; set; } = 6.987654F;

        [DisplayName("Enum")]
        [Description("This is an enumerable.")]
        [DefaultValue(SampleEnum.ILoveIt)]
        public SampleEnum Reloaded { get; set; } = SampleEnum.ILoveIt;

        public enum SampleEnum
        {
            NoOpinion,
            Sucks,
            IsMediocre,
            IsOk,
            IsCool,
            ILoveIt
        }
         */
    }
}
