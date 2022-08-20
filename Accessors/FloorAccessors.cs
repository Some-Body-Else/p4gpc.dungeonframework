using Reloaded.Hooks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Hooks.Definitions.X86;
using Reloaded.Memory.Sources;
using Reloaded.Memory;
using Reloaded.Memory.Sigscan;
using Reloaded.Mod.Interfaces;

using static Reloaded.Hooks.Definitions.X86.FunctionAttribute;

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

namespace p4gpc.dungeonloader.Accessors
{
    public class FloorAccessors
    {

        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private List<IReverseWrapper> _reverseWrapperList;
        private List<IAsmHook> _functionHookList;
        private List<DungeonFloors> _dungeonFloors;
        private List<String> _commands;

        /**
         * An idea for refactoring is to, instead of replacing commands, redirect the address values pointing to 0x21EB4AA0
         * to an address referring to a space set aside by our mod, thereby allowing for any amount of entries to be added while
         * minimizing the amount of code changes needed. Going to keep moving with the command replacement because its already
         * started and because it might give insight into other parts of the code we may need to change for something like
         * dungeon gen or dungeon room data
         *
         */
        public FloorAccessors(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _dungeonFloors = _jsonImporter.GetFloors();
            _commands = new List<String>();


            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
        }
        private void Initialize()
        {
            List<String> functions = _jsonImporter.getFloorFunctions();
            long currentAddress;
            IReverseWrapper<GetIdFunction> reverseWrapperID = _hooks.CreateReverseWrapper<GetIdFunction>(GetID);
            IReverseWrapper<GetSubIdFunction> reverseWrapperSubID = _hooks.CreateReverseWrapper<GetSubIdFunction>(GetSubID);
            IReverseWrapper<GetByte04Function> reverseWrapperByte04 = _hooks.CreateReverseWrapper<GetByte04Function>(GetByte04);
            IReverseWrapper<GetFloorMinFunction> reverseWrapperFloorMin = _hooks.CreateReverseWrapper<GetFloorMinFunction>(GetFloorMax);
            IReverseWrapper<GetFloorMaxFunction> reverseWrapperFloorMax = _hooks.CreateReverseWrapper<GetFloorMaxFunction>(GetFloorMin);
            IReverseWrapper<GetByte0AFunction> reverseWrapperByte0A = _hooks.CreateReverseWrapper<GetByte0AFunction>(GetByte0A);
            IReverseWrapper<GetScriptIdFunction> reverseWrapperScript = _hooks.CreateReverseWrapper<GetScriptIdFunction>(GetScriptID);
            IReverseWrapper<GetEnvIdFunction> reverseWrapperEnv = _hooks.CreateReverseWrapper<GetEnvIdFunction>(GetEnvID);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetID, out reverseWrapperID)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetSubID, out reverseWrapperSubID)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetByte04, out reverseWrapperByte04)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetFloorMin, out reverseWrapperFloorMin)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetFloorMax, out reverseWrapperFloorMax)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetByte0A, out reverseWrapperByte0A)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetScriptID, out reverseWrapperScript)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetEnvID, out reverseWrapperEnv)}");
            _reverseWrapperList.Add(reverseWrapperID);
            _reverseWrapperList.Add(reverseWrapperSubID);
            _reverseWrapperList.Add(reverseWrapperByte04);
            _reverseWrapperList.Add(reverseWrapperFloorMin);
            _reverseWrapperList.Add(reverseWrapperFloorMax);
            _reverseWrapperList.Add(reverseWrapperByte0A);
            _reverseWrapperList.Add(reverseWrapperScript);
            _reverseWrapperList.Add(reverseWrapperEnv);
            currentAddress = _utils.SigScan(functions[0], "StaticFloorDungeonBin");
            SetupStaticFloorDungeonBin((int)(currentAddress & 0xFFFFFFFF), functions[0]);


            currentAddress = _utils.SigScan(functions[1], "RandomFloorDungeonBin");
            SetupRandomFloorDungeonBin((int)(currentAddress & 0xFFFFFFFF), functions[1]);

            currentAddress = _utils.SigScan(functions[2], "ScriptHandlingDungeonBin");
            SetupScriptHandling((int)(currentAddress & 0xFFFFFFFF), functions[2]);

            currentAddress = _utils.SigScan(functions[3], "EnvHandlingDungeonBin");
            SetupEnvHandling((int)(currentAddress & 0xFFFFFFFF), functions[3]);
        }

        private void SetupStaticFloorDungeonBin(int functionAddress, string pattern)
        {
            //tracks ID in register eix
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"mov edx, edi");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[5]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"or eax, 0x80000000");
            instruction_list.Add($"mov [ebx+0x24], eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[2]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[4]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[3]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");


            instruction_list.Add($"mov eax, [ebx+0x24]");
            instruction_list.Add($"push eax");
            instruction_list.Add($"mov eax, [ebx+8]");
            instruction_list.Add($"push 0");
            instruction_list.Add($"push 0");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");


            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ecx, eax");

            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[1]);
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"push eax");
            _functionHookList.Add( _hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate() );

        }

        private void SetupRandomFloorDungeonBin(int functionAddress, string pattern)
        {
            //tracks ID in register edx
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[5]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"or eax, 0x80000000");
            instruction_list.Add($"mov [ebx+0x24], eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[2]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[4]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[3]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");


            instruction_list.Add($"mov eax, [ebx+0x24]");
            instruction_list.Add($"push eax");
            instruction_list.Add($"mov eax, [ebx+8]");
            instruction_list.Add($"push 0");
            instruction_list.Add($"push 0");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");


            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ecx, eax");

            instruction_list.Add($"push edx");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[1]);
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"mov ecx, esi");
            instruction_list.Add($"push eax");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }

        private void SetupScriptHandling(int functionAddress, string pattern)
        {

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push edx");
            instruction_list.Add($"mov edx, eax");
            instruction_list.Add($"sub edx, 0x21EB4AA0");
            instruction_list.Add($"shr edx, 4");
            instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[6]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"push eax");
            instruction_list.Add($"lea eax,[ebp-0x000000A4]");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }

        private void SetupEnvHandling(int functionAddress, string pattern)
        {

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push edx");
            instruction_list.Add($"mov edx, ecx");
            instruction_list.Add($"sub edx, 0x21EB4AA0");
            instruction_list.Add($"shr edx, 4");
            //instruction_list.Add($"push ecx");
            instruction_list.Add(_commands[7]);
            //instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop edi");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"pop ebx"); 
            instruction_list.Add($"mov ecx,[ebp-04]");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());

        }

        private int GetID(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].ID;
        }
        private int GetSubID(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].subID;
        }
        private int GetByte04(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].Byte04;
        }
        private int GetFloorMin(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].floorMin;
        }
        private int GetFloorMax(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].floorMax;
        }
        private int GetByte0A(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].Byte0A;
        }
        private int GetScriptID(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].dungeonScript;
        }
        private int GetEnvID(int entryID)
        {
            entryID /= 2;
            return _dungeonFloors[entryID].usedEnv;
        }

        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetIdFunction(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetSubIdFunction(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetByte04Function(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetFloorMinFunction(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetFloorMaxFunction(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetByte0AFunction(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetScriptIdFunction(int edx);
        [Function(Register.edx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetEnvIdFunction(int edx);
    }

}
