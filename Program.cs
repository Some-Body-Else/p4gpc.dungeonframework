using p4gpc.dungeonloader.Configuration;
using p4gpc.dungeonloader.Configuration.Implementation;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Memory.Sources;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

using System;
using System.Diagnostics;

namespace p4gpc.dungeonloader
{
    public class Program : IMod
    {
        /// <summary>
        /// Not quite sure, I'm stealing this from Swine
        /// </summary>
        private const string MyModId = "p4gpc.dungeonloader";

        /// <summary>
        /// Used for writing text to the Reloaded log.
        /// </summary>
        private ILogger _logger;

        /// <summary>
        /// Provides access to the mod loader API.
        /// </summary>
        private IModLoader _modLoader;

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


        // Encapsulates your mod logic.
        private DungeonLoader _dungeonloader;

        // Accesses memory of our running process
        private IMemory _memory;

        private Utilities _utilities;

        public void StartEx(IModLoaderV1 loaderApi, IModConfigV1 modConfig)
        {
            //Debugger.Launch();

            _modLoader = (IModLoader)loaderApi;
            _modConfig = (IModConfig)modConfig;
            _logger = (ILogger)_modLoader.GetLogger();
            _modLoader.GetController<IReloadedHooks>().TryGetTarget(out _hooks!);


            //_logger.WriteLine($"[DungeonLoader] Successfully attached to Reloaded-II logger");
            
            var configurator = new Configurator(_modLoader.GetModConfigDirectory(_modConfig.ModId));
            _configuration = configurator.GetConfiguration<Config>(0);
            _configuration.ConfigurationUpdated += OnConfigurationUpdated;


            _memory = new Memory();
            using var currentProc = Process.GetCurrentProcess();
            int baseAddress = currentProc.MainModule.BaseAddress.ToInt32();
            _utilities = new Utilities(_configuration, _logger, baseAddress);
            _dungeonloader = new DungeonLoader(_hooks, _utilities, _memory, _configuration);
        }

        private void OnConfigurationUpdated(IConfigurable obj)
        {
            _configuration = (Config)obj;
            _logger.WriteLine($"[{_modConfig.ModId}] Config Updated: Applying");

            _dungeonloader._configuration = _configuration;
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