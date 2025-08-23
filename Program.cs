using p4gpc.dungeonframework.Configuration;
using p4gpc.dungeonframework.Accessors;
using p4gpc.dungeonframework.Configuration.Implementation;
using p4gpc.dungeonframework.JsonClasses;

using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace p4gpc.dungeonframework
{
    public class Program : IMod
    {
        /// <summary>
        /// Not quite sure, I'm stealing this from Swine
        /// </summary>
        private const string MyModId = "p4gpc64.dungeonframework";

        /// <summary>
        /// Used for writing text to the Reloaded log.
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// Provides access to the mod loader API.
        /// </summary>
        public IModLoader _modLoader;

        /// <summary>
        /// Stores the contents of your mod's configuration. Automatically updated by template.
        /// </summary>
        private Config _configuration;

        /// <summary>
        /// An interface to Reloaded's the function hooks/detours library.
        /// See: https://github.com/Reloaded-Project/Reloaded.Hooks
        ///      for documentation and samples. 
        /// </summary>
        private IReloadedHooks _hooks;

        /// <summary>
        /// Configuration of the current mod.
        /// </summary>
        private IModConfig _modConfig;
        // Accesses memory of our running process
        private IMemory _memory;

        private JsonImporter _jsonImporter;

        private List<Accessor> _accessors;

        private Utilities _utilities;

        string? custom_json_path;

        public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
        {
            

            _modLoader = (IModLoader)loaderApi;
            _modConfig = (IModConfig)modConfig;
            _logger = (ILogger)_modLoader.GetLogger();
            _modLoader.GetController<IReloadedHooks>().TryGetTarget(out _hooks!);
            
            var configurator = new Configurator(_modLoader.GetModConfigDirectory(_modConfig.ModId));
            _configuration = configurator.GetConfiguration<Config>(0);
            _configuration.ConfigurationUpdated += OnConfigurationUpdated;
            

            _memory = new Memory();
            using var currentProc = Process.GetCurrentProcess();
            
            Int64 baseAddress = currentProc.MainModule.BaseAddress.ToInt64();
            string defaultPath = Path.GetFullPath(_modLoader.GetDirectoryForModId(_modConfig.ModId) + "\\JSON");

            _utilities = new Utilities(_configuration, _logger, baseAddress);
            _jsonImporter = new JsonImporter(_configuration, _utilities, defaultPath, true);
            _utilities.Log("JSON files loaded.");



            this._modLoader.ModLoading += this.OnModLoading;
            this._modLoader.OnModLoaderInitialized += this.UpdateModData;

            _accessors = new List<Accessor>();
            _accessors.Add(new TemplateTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Add(new FloorTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Add(new EncountTables(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Add(new RoomTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Add(new MinimapTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Add(new FieldComparesAccessor(_hooks, _utilities, _memory, _configuration, _jsonImporter));

            _utilities.Log("DungeonFramework set up complete!");
        }

        private void OnModLoading(IModV1 mod, IModConfigV1 config)
        {
            var modDir = this._modLoader.GetDirectoryForModId(config.ModId);
            var dungeon_JSON_dir = Path.Join(modDir, "dungeonframework");
            if (Directory.Exists(dungeon_JSON_dir))
            {
                _utilities.Log(dungeon_JSON_dir);
                custom_json_path = dungeon_JSON_dir;
            }

        }

        private void UpdateModData()
        {
            if (custom_json_path != null)
            {
                _jsonImporter = new JsonImporter(_configuration, _utilities, custom_json_path, false);
                foreach (Accessor accessor in _accessors)
                {
                    accessor.updateAccessor(_jsonImporter);
                }
                _utilities.Log("DungeonFramework JSON contents updated!");
            }
        }

        private void OnConfigurationUpdated(IConfigurable obj)
        {
            _configuration = (Config)obj;
            _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");

            //_templates._configuration = _configuration;
        }

        /* Mod loader actions. */
        public void Suspend()
        {
            /*  Some tips if you wish to support this (CanSuspend == true)

                A. Undo memory modifications.
                B. Deactivate hooks. (Reloaded.Hooks Supports This!)
            */
        }

        public void Resume()
        {
            /*  Some tips if you wish to support this (CanSuspend == true)

                A. Redo memory modifications.
                B. Re-activate hooks. (Reloaded.Hooks Supports This!)
            */
        }

        public void Unload()
        {
            /*  Some tips if you wish to support this (CanUnload == true).

                A. Execute Suspend(). [Suspend should be reusable in this method]
                B. Release any unmanaged resources, e.g. Native memory.
            */
        }

        /*  If CanSuspend == false, suspend and resume button are disabled in Launcher and Suspend()/Resume() will never be called.
            If CanUnload == false, unload button is disabled in Launcher and Unload() will never be called.
        */
        public bool CanUnload() => false;
        public bool CanSuspend() => false;

        /* Automatically called by the mod loader when the mod is about to be unloaded. */
        public Action Disposing { get; } = null!;
    }
}