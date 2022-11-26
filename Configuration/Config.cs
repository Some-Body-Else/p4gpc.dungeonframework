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
        [Description("Allows DungeonLoader to load in user-created JSON files to find code to replace in the game.\n"+
                     "NOTE: Unless the user intends to replace more of the executable's original code with their own, this should be disabled."
                     )]
        public bool customSearch { get; set; } = false;

        [DisplayName("Suppress default warning/error text")]
        [Description("DungeonLoader will present warning/error text in the Reloaded-II log when it cannot find a custom JSON to load.\n" +
                     "After displaying the text, it will load in a default JSON in place of the missing one that replicates vanilla dungeon behavior.\n" + 
                     "Setting this to true will disable the warning/error text from being logged when a default file is used instead of an expected custom file."
                    )]
        public bool suppressWarnErr { get; set; } = false;

        [DisplayName("Allow debug logs")]
        [Description("Will allow DungeonLoader to log debug information to the Reloaded-II console.\n"+
                     "Information logged includes addresses of files in RAM, among other program details.\n"+
                     "This information not required for usage, so it is disabled by default."
                     )]
        public bool logDebug { get; set; } = true;

        [DisplayName("Allow debug logs")]
        [Description("Will allow DungeonLoader to log debug information to the Reloaded-II console.\n"+
                     "Information logged includes addresses of files in RAM, among other program details.\n"+
                     "This information not required for usage, so it is disabled by default."
                     )]
        public bool noteSizeDiscrepency { get; set; } = true;



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
