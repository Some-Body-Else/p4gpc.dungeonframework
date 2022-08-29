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
using Newtonsoft.Json.Linq;

namespace p4gpc.dungeonloader.Accessors
{
    public  class FieldCompareAccessors
    {
        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private List<IReverseWrapper> _reverseWrapperList;
        private List<IAsmHook> _functionHookList;
        private FieldCompare _fieldCompare;
        private List<String> _commands;

        public FieldCompareAccessors(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _fieldCompare = _jsonImporter.GetFieldCompare();
            _commands = new List<String>();

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
            _utils.Log("Field compare-adjacent hooks established.");
        }

        private void Initialize()
        {
            Debugger.Launch();

            List<String> functions = _jsonImporter.GetFieldCompareFunctions();
            long address;
            List<long> addressList;

            IReverseWrapper<AccessRoomTypeTableFunction> reverseWrapperAccessRoomTypeTable = _hooks.CreateReverseWrapper<AccessRoomTypeTableFunction>(AccessRoomTypeTable);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(AccessRoomTypeTable, out reverseWrapperAccessRoomTypeTable)}");
            _reverseWrapperList.Add(reverseWrapperAccessRoomTypeTable);

            addressList = _utils.SigScan_FindAll(functions[0], "FieldCompareFunc1");
            foreach (long value in addressList)
            {
                SetupFieldCompareOne((int)value, functions[0]);
            }
            address =_utils.SigScan(functions[1], "FieldCompareFunc2");
            SetupFieldCompareTwo((int)address, functions[1]);


            address =_utils.SigScan(functions[2], "FieldCompareFunc3");
            SetupFieldCompareThree((int)address, functions[2]);
        }

        private void SetupFieldCompareOne(int functionAddress, string pattern)
        {
            string[] splitPattern = pattern.Split(" ");
            int jumpToAddress = functionAddress + _utils.GetPatternLength(pattern) + Convert.ToInt32(splitPattern.Last(), 16);
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"je return_normal");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"je return_normal");
            instruction_list.Add($"push {jumpToAddress}");
            instruction_list.Add($"ret"); //jumps to jumpToAddress properly

            instruction_list.Add($"label return_normal");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupFieldCompareTwo(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"cmp eax, 0xFFFF");
            instruction_list.Add($"je return_invalid");

            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, eax");
            instruction_list.Add($"mov [ebp-8], dword 0");
            instruction_list.Add($"mov [ebp-4], dword 0");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"je return_normal");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"je return_normal");
            instruction_list.Add($"label return_invalid");

            instruction_list.Add($"push 0x247CEADC");
            instruction_list.Add($"ret");
            instruction_list.Add($"label return_normal");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupFieldCompareThree(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, edi");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"je pregen_check");


            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, edi");
            instruction_list.Add($"mov ecx, esi");
            instruction_list.Add($"shl ecx, 24");
            instruction_list.Add($"shr ecx, 24");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 0");
            instruction_list.Add($"je branch_one");


            instruction_list.Add($"mov eax, ecx");
            instruction_list.Add($"jmp third_compare");

            instruction_list.Add($"label branch_one");
            instruction_list.Add($"push 0x25C81F8F");
            instruction_list.Add($"ret");

            instruction_list.Add($"label branch_two");
            instruction_list.Add($"push 0x25C81E95");
            instruction_list.Add($"ret");

            instruction_list.Add($"label pregen_check");
            instruction_list.Add($"shl ebx, 16");
            instruction_list.Add($"shr ebx, 16");
            instruction_list.Add($"test ebx, ebx");
            instruction_list.Add($"jne third_compare");



            instruction_list.Add($"label is_pregen");
            instruction_list.Add($"mov eax, edi");
            instruction_list.Add($"shl eax, 24");
            instruction_list.Add($"shr eax, 24");


            instruction_list.Add($"label third_compare");
            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, eax");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"je branch_two");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());



        }
        private RoomLoadType AccessRoomTypeTable(int value)
        {
            return _fieldCompare.fieldArray[value];
        }

        [Function(Register.esi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate RoomLoadType AccessRoomTypeTableFunction(int esi);
    }
}
