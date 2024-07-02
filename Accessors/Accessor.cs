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

using p4gpc.dungeonframework.Exceptions;
using p4gpc.dungeonframework.JsonClasses;
using p4gpc.dungeonframework.Configuration;
using System.ComponentModel.Design;
using System.Reflection.Metadata.Ecma335;

namespace p4gpc.dungeonframework.Accessors
{
    public class Accessor
    {

        protected static nuint _newMinimapLookupTable;
        protected static nuint _newMinimapPathLookupTable;
        protected static nuint _lastUsedAddress = 0;
        protected IReloadedHooks? _hooks;
        protected Utilities? _utils;
        protected IMemory _memory;
        protected Config _configuration;
        protected JsonImporter _jsonImporter;
        protected List<IReverseWrapper> _reverseWrapperList;
        protected List<IAsmHook> _functionHookList;
        protected List<String> _commands;


        /// <summary>
        /// Distinct from Reloaded.Hooks.Definitions.X64.FunctionAttribute Register enum due to ordering the registers differently.<br></br>
        /// Reloaded's Register orders the letter-based registers in alphabetical order (rax = 0, rbx = 1, rcx = 2, rdx = 3)<br></br>
        /// Issue is that x86/x64 doesn't use that ordering for internally identifying registers; it uses (rax = 0, rcx = 1, rdx = 2, rbx = 3)<br></br>
        /// God knows why, but since this program needs to read from the opcodes, AccessorRegister is used to keep the order.<br></br>
        /// </summary>
        protected enum AccessorRegister 
        {
            rax = 0,    //0000
            rcx = 1,    //0001
            rdx = 2,    //0010
            rbx = 3,    //0011
            rsp = 4,    //0100
            rbp = 5,    //0101
            rsi = 6,    //0110
            rdi = 7,    //0111
            r8  = 8,    //1000
            r9  = 9,    //1001
            r10 = 10,   //1010
            r11 = 11,   //1011
            r12 = 12,   //1100
            r13 = 13,   //1101
            r14 = 14,   //1110
            r15 = 15    //1111
        }

        protected enum Size
        {
            Byte        = 1,
            Word        = 2,
            DoubleWord  = 3
        }

        protected const byte DOUBLEWORD = 8;
        protected const byte WORD = 4;
        protected const byte HALFWORD = 2;
        protected const byte BYTE = 1;
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

            if (_lastUsedAddress == 0)
            {
                _lastUsedAddress = _memory.Allocate(8);
                _utils.LogDebug($"Debug address: {_lastUsedAddress.ToString("X8")}", Config.DebugLevels.CodeReplacedLocations);
            }    

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
        }

        protected virtual void Initialize(){}
    }
}
