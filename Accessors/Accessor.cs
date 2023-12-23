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

        protected static nuint _newMinimapLookupTable;
        protected static nuint _newMinimapPathLookupTable;
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
        
        /*
        protected Dictionary<Register, String> AccReg_64;
        protected Dictionary<Register, String> AccReg_32;
        protected Dictionary<Register, String> AccReg_16;
        protected Dictionary<Register, String> AccReg_8;
         */

        protected enum Size
        {
            Byte        = 1,
            Word        = 2,
            DoubleWord  = 3
        }

        protected Accessor()
        {
            /*
            AccReg_64 = new Dictionary<Register, String>();
            AccReg_32 = new Dictionary<Register, String>();
            AccReg_16 = new Dictionary<Register, String>();
            AccReg_8 = new Dictionary<Register, String>();
            AccReg_64.Add(Register.rax, "rax");
            AccReg_32.Add(Register.rax, "eax");
            AccReg_16.Add(Register.rax, "ax");
            AccReg_8.Add(Register.rax, "al");
            AccReg_64.Add(Register.rcx, "rcx");
            AccReg_32.Add(Register.rcx, "ecx");
            AccReg_16.Add(Register.rcx, "cx");
            AccReg_8.Add(Register.rcx, "cl");
            AccReg_64.Add(Register.rdx, "rdx");
            AccReg_32.Add(Register.rdx, "edx");
            AccReg_16.Add(Register.rdx, "dx");
            AccReg_8.Add(Register.rdx, "dl");
            AccReg_64.Add(Register.rbx, "rbx");
            AccReg_32.Add(Register.rbx, "ebx");
            AccReg_16.Add(Register.rbx, "bx");
            AccReg_8.Add(Register.rbx, "bl");
            AccReg_64.Add(Register.rsp, "rsp");
            AccReg_32.Add(Register.rsp, "esp");
            AccReg_16.Add(Register.rsp, "sp");
            AccReg_8.Add(Register.rsp, "spl");
            AccReg_64.Add(Register.rbp, "rbp");
            AccReg_32.Add(Register.rbp, "ebp");
            AccReg_16.Add(Register.rbp, "bp");
            AccReg_8.Add(Register.rbp, "bpl");
            AccReg_64.Add(Register.rsi, "rsi");
            AccReg_32.Add(Register.rsi, "esi");
            AccReg_16.Add(Register.rsi, "si");
            AccReg_8.Add(Register.rsi, "sil");
            AccReg_64.Add(Register.rdi, "rdi");
            AccReg_32.Add(Register.rdi, "edi");
            AccReg_16.Add(Register.rdi, "di");
            AccReg_8.Add(Register.rdi, "dil");
            AccReg_64.Add(Register.r8, "r8");
            AccReg_32.Add(Register.r8, "r8d");
            AccReg_16.Add(Register.r8, "r8w");
            AccReg_8.Add(Register.r8, "r8l");
            AccReg_64.Add(Register.r9, "r9");
            AccReg_32.Add(Register.r9, "r9d");
            AccReg_16.Add(Register.r9, "r9w");
            AccReg_8.Add(Register.r9, "r9l");
            AccReg_64.Add(Register.r10, "r10");
            AccReg_32.Add(Register.r10, "r10d");
            AccReg_16.Add(Register.r10, "r10w");
            AccReg_8.Add(Register.r10, "r10l");
            AccReg_64.Add(Register.r11, "r11");
            AccReg_32.Add(Register.r11, "r11d");
            AccReg_16.Add(Register.r11, "r11w");
            AccReg_8.Add(Register.r11, "r11l");
            AccReg_64.Add(Register.r12, "r12");
            AccReg_32.Add(Register.r12, "r12d");
            AccReg_16.Add(Register.r12, "r12w");
            AccReg_8.Add(Register.r12, "r12l");
            AccReg_64.Add(Register.r13, "r13");
            AccReg_32.Add(Register.r13, "r13d");
            AccReg_16.Add(Register.r13, "r13w");
            AccReg_8.Add(Register.r13, "r13l");
            AccReg_64.Add(Register.r14, "r14");
            AccReg_32.Add(Register.r14, "r14d");
            AccReg_16.Add(Register.r14, "r14w");
            AccReg_8.Add(Register.r14, "r14l");
            AccReg_64.Add(Register.r15, "r15");
            AccReg_32.Add(Register.r15, "r15d");
            AccReg_16.Add(Register.r15, "r15w");
            AccReg_8.Add(Register.r15, "r15l"); 
             */
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

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
        }

        protected virtual void Initialize(){}
    }
}
