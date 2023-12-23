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

        [DisplayName("Suppress default warning/error text")]
        [Description("DungeonLoader will present warning/error text in the Reloaded-II log when it cannot find a custom JSON to load.\n" +
                     "After displaying the text, it will load in a default JSON in place of the missing one that replicates vanilla dungeon behavior.\n" +
                     "Enabling this setting will disable the warning/error text from being logged when a default file is used instead of an expected custom file."
                    )]
        public bool suppressWarnErr { get; set; } = false;

        [DisplayName("Debug level")]
        [Description("Will allow DungeonLoader to log debug information to the Reloaded-II console.\n"+
                     "Information logged includes addresses of files in RAM, among other program details.\n"+
                     "This information not required for usage, so it is disabled by default.\n"+
                     "Each level of debug also shows the previous levels, if applicable."
                     )]
        [DefaultValue(DebugLevels.NoMessages)]
        public DebugLevels logDebug { get; set; } = DebugLevels.NoMessages;

        public enum DebugLevels
        {
            NoMessages,
            AlertConnections,
            TableLocations,
            CodeReplacedLocations
        }




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

        
         */
    }
}
