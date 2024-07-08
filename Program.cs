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

        public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
        {
            // Debugger.Launch();

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
            
            string modPath = Path.GetFullPath(Path.Combine(currentProc.MainModule.FileName, @"..\\dungeonframework"));
            string defaultPath = Path.GetFullPath(Path.Combine(_modLoader.GetModConfigDirectory(_modConfig.ModId), @"..\\..\\..\\")) + "\\Mods\\p4gpc.dungeonframework\\JSON";

            _utilities = new Utilities(_configuration, _logger, baseAddress);
            _jsonImporter = new JsonImporter(_configuration, _utilities, modPath, defaultPath);
            _utilities.Log("JSON files loaded.");

            // Debugger.Launch();
            _accessors = new List<Accessor>();
            _accessors.Append(new TemplateTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Append(new FloorTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Append(new EncountTables(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Append(new RoomTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Append(new MinimapTable(_hooks, _utilities, _memory, _configuration, _jsonImporter));
            _accessors.Append(new RoomCompares(_hooks, _utilities, _memory, _configuration, _jsonImporter));

            // Field comparison replacements are a larger-scale thing I want to tackle later.
            // What type of field you deal with (overworld, dungeon, battle, other?) is dictated by what
            // range of number its ID falls into. As you might imagine, this makes the possibility of creating
            // custom fields more difficult, since instead of just using an unused ID, you'd have to find one
            // in a specific range instead. However, for the moment, I'm focusing on dungeon-only stuff, so
            // this is to be ignored for the moment.
            // _accessors.Append(new FieldCompares(_hooks, _utilities, _memory, _configuration, _jsonImporter));

            _utilities.Log("DungeonLoader set up complete!");
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