using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.Sources;
using Reloaded.Memory;
using Reloaded.Memory.Sigscan;
using Reloaded.Mod.Interfaces;

using static Reloaded.Hooks.Definitions.X64.FunctionAttribute;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Diagnostics;

using p4gpc.dungeonloader.Exceptions;
using p4gpc.dungeonloader.JsonClasses;
using p4gpc.dungeonloader.Configuration;
using System.ComponentModel.Design;
using System.Reflection.Metadata.Ecma335;

namespace p4gpc.dungeonloader.Accessors
{
    public class Accessor
    {
        protected IReloadedHooks? _hooks;
        protected Utilities? _utils;
        protected IMemory _memory;
        protected Config _configuration;
        protected JsonImporter _jsonImporter;
        protected List<IReverseWrapper> _reverseWrapperList;
        protected List<IAsmHook> _functionHookList;
        protected List<String> _commands;

        protected Accessor()
        {
        }

        protected void executeAccessor(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _commands = new List<String>();

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());

        }

        protected virtual void Initialize(){}
    }
}
