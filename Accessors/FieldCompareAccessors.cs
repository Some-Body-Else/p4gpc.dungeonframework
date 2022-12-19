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
using static p4gpc.dungeonloader.Accessors.FieldCompareAccessors;

namespace p4gpc.dungeonloader.Accessors
{
    public  class FieldCompareAccessors
    {
        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private Dictionary<int, int> _dungeon_template_dict;
        private List<DungeonFloors> _dungeonFloors;
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
            _dungeon_template_dict = _jsonImporter.GetDungeonTemplateDictionary();
            _dungeonFloors = _jsonImporter.GetFloors();
            _commands = new List<String>();

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
            _utils.Log("Field compare hooks established.");
        }

        private void Initialize()
        {
            //Debugger.Launch();

            List<String> functions = _jsonImporter.GetFieldCompareFunctions();
            long address;
            List<long> addressList;

            IReverseWrapper<AccessRoomTypeTableFunction> reverseWrapperAccessRoomTypeTable = _hooks.CreateReverseWrapper<AccessRoomTypeTableFunction>(AccessRoomTypeTable);
            IReverseWrapper<GetDungeonTemplateIDFunction> reverseWrapperGetDungoenTemplateID = _hooks.CreateReverseWrapper<GetDungeonTemplateIDFunction>(GetDungeonTemplateID);
            IReverseWrapper<StaticFloorCheckFunction> reverseWrapperStaticFloorCheck = _hooks.CreateReverseWrapper<StaticFloorCheckFunction>(StaticFloorCheck);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(AccessRoomTypeTable, out reverseWrapperAccessRoomTypeTable)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetDungeonTemplateID, out reverseWrapperGetDungoenTemplateID)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(StaticFloorCheck, out reverseWrapperStaticFloorCheck)}");
            _reverseWrapperList.Add(reverseWrapperAccessRoomTypeTable);
            _reverseWrapperList.Add(reverseWrapperGetDungoenTemplateID);
            _reverseWrapperList.Add(reverseWrapperStaticFloorCheck);

            addressList = _utils.SigScan_FindAll(functions[0], "FieldCompareFunc0");
            foreach (long value in addressList)
            {
                SetupFieldCompareOne((int)value, functions[0]);
            }
            address =_utils.SigScan(functions[1], "FieldCompareFunc2");
            SetupFieldCompareTwo((int)address, functions[1]);


            address =_utils.SigScan(functions[2], "FieldCompareFunc3");
            SetupFieldCompareThree((int)address, functions[2]);

            addressList = _utils.SigScan_FindAll(functions[3], "TemplateGetReplacements");
            foreach (long value in addressList)
            {
                SetupTemplateGetReplacement((int)value, functions[3]);
            }

            address =_utils.SigScan(functions[4], "FieldCompareFunc4");
            SetupFieldCompareFour((int)address, functions[4]);

            address =_utils.SigScan(functions[5], "FieldCompareFunc5");
            SetupFieldCompareFive((int)address, functions[5]);

            address =_utils.SigScan(functions[6], "FieldCompareFunc6");
            SetupFieldCompareSix((int)address, functions[6]);

            address =_utils.SigScan(functions[7], "FieldCompareFunc7");
            SetupFieldCompareSeven((int)address, functions[7]);
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
            instruction_list.Add($"add esp, 0xC");
            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, edi");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"jne static_check");

            instruction_list.Add($"shl ebx, 16");
            instruction_list.Add($"shr ebx, 16");
            instruction_list.Add($"test ebx, ebx");
            instruction_list.Add($"jne static_check");
            instruction_list.Add($"jmp third_compare");


            instruction_list.Add($"label static_check");
            instruction_list.Add($"mov ecx, edi");
            instruction_list.Add($"shl ecx, 24");
            instruction_list.Add($"shr ecx, 24");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"jne branch_one");
            instruction_list.Add($"jmp third_compare");

            instruction_list.Add($"label branch_one");
            instruction_list.Add($"push 0x25C81F8F");
            instruction_list.Add($"ret");

            instruction_list.Add($"label branch_two");
            instruction_list.Add($"mov eax, edi");
            instruction_list.Add($"sub eax, 0x3C");
            instruction_list.Add($"shl eax, 16");
            instruction_list.Add($"shr eax, 16");

            instruction_list.Add($"push 0x25C81ED8");
            instruction_list.Add($"ret");



            instruction_list.Add($"label third_compare");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"jne branch_two");
            instruction_list.Add($"mov eax, edi");
            instruction_list.Add($"sub eax, 0x3C");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupFieldCompareFour(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"je random_field");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"je pregen_field");
            instruction_list.Add($"push 0x24B12C31");
            instruction_list.Add($"ret");

            instruction_list.Add($"label random_field");
            instruction_list.Add($"mov eax, ebx");
            instruction_list.Add($"shl eax, 16");
            instruction_list.Add($"shr eax, 16");

            instruction_list.Add($"push 0x24B12BC9");
            instruction_list.Add($"ret");


            instruction_list.Add($"label pregen_field");
            instruction_list.Add($"mov eax, ebx");
            instruction_list.Add($"shl eax, 16");
            instruction_list.Add($"shr eax, 16");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupFieldCompareFive(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, ecx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"jne not_random");

            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, [esi+4]");
            instruction_list.Add($"cmp esi, 0");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"je random");

            instruction_list.Add($"label not_random");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"jne not_pregen");
            instruction_list.Add($"jmp regular_path");

            instruction_list.Add($"label random");
            instruction_list.Add($"push 0x24B1BB9C");
            instruction_list.Add($"ret");

            instruction_list.Add($"label not_pregen");
            instruction_list.Add($"push 0x24B1BB9C");
            instruction_list.Add($"ret");

            instruction_list.Add($"label regular_path");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupFieldCompareSix(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"je pregen");
            instruction_list.Add($"push 0x24B12D83");
            instruction_list.Add($"ret");

            instruction_list.Add($"label pregen");
            instruction_list.Add($"xor eax, eax");
            instruction_list.Add($"label battle");
            instruction_list.Add($"xor ecx, ecx");
            instruction_list.Add($"mov [ebp-0x4C], ecx");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupFieldCompareSeven(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[2]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"jne nonstatic_floor");
            instruction_list.Add($"push 0x247EFECD");
            instruction_list.Add($"ret");

            instruction_list.Add($"label nonstatic_floor");
            instruction_list.Add($"mov edx, 0x00AFA558");

            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, ecx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[0]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"cmp eax, 1");
            instruction_list.Add($"jne pregen_check");

            instruction_list.Add($"push esi");
            instruction_list.Add($"mov esi, [edx+4]");
            instruction_list.Add($"cmp esi, 0");
            instruction_list.Add($"pop esi");
            instruction_list.Add($"je end_of_func");

            instruction_list.Add($"label pregen_check");
            instruction_list.Add($"cmp eax, 2");
            instruction_list.Add($"jne not_pregen");
            instruction_list.Add($"jmp end_of_func");

            instruction_list.Add($"label not_pregen");
            instruction_list.Add($"push 0x247EFECD");
            instruction_list.Add($"ret");

            instruction_list.Add($"label end_of_func");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void SetupTemplateGetReplacement(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            instruction_list.Add($"{_commands[1]}");
            instruction_list.Add($"pop edx");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"lea eax, [eax*2 + eax]");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private int StaticFloorCheck(int floorID)
        {
            if (!_dungeonFloors.Any(floor => floor.ID == floorID))
            {
                return 1; 
            }
            if (_fieldCompare.fieldArray[floorID] == 0)
            {
                return 1;
            }
            return 0;
        }

        private RoomLoadType AccessRoomTypeTable(int value)
        {
            return _fieldCompare.fieldArray[value];
        }
        private int GetDungeonTemplateID(int dungeonID)
        {
            return _dungeon_template_dict[dungeonID];
        }

        [Function(Register.esi, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate RoomLoadType AccessRoomTypeTableFunction(int esi);

        [Function(Register.ecx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetDungeonTemplateIDFunction(int ecx);


        [Function(Register.ecx, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int StaticFloorCheckFunction(int ecx);
    }
}
