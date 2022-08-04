using p4gpc.dungeonloader.Configuration.Implementation;
using System.ComponentModel;

namespace p4gpc.dungeonloader.Configuration
{
    public class Config : Configurable<Config>
    {
        /*
            User Properties:
                - Please put all of your configurable properties here.
        
            By default, configuration saves as "Config.json" in mod user config folder.    
            Need more config files/classes? See Configuration.cs
        
            Available Attributes:
            - Category
            - DisplayName
            - Description
            - DefaultValue

            // Technically Supported but not Useful
            - Browsable
            - Localizable

            The `DefaultValue` attribute is used as part of the `Reset` button in Reloaded-Launcher.
        */
        [DisplayName("Json Folder Path")]
        [Description("Path that mod expects to find JSON files within.")]
        [DefaultValue("JSON folder within mod's folder")]
        public string Json_Folder_Path { get; set; } = (System.IO.Directory.GetCurrentDirectory() + "/Mods/p4gpc.dungeonloader/JSON");

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
    }
}
