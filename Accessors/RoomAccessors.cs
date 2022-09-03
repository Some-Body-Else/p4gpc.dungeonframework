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
    public class RoomAccessors
    {

        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private List<IReverseWrapper> _reverseWrapperList;
        private List<IAsmHook> _functionHookList;
        private List<DungeonRooms> _dungeonRooms;
        private List<String> _commands;

        private enum RoomPushType
        {
            PUSH_ONLY,
            ADD_COMMANDS_1,
            ADD_COMMANDS_2
        }

        public RoomAccessors(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _dungeonRooms = _jsonImporter.GetRooms();
            _commands = new List<String>();


            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
            _utils.Log("Room-adjacent hooks established.");
        }

        private void Initialize()
        {
            IReverseWrapper<LeftHandShiftFunction> reverseWrapperLeftHandShiftFunction = _hooks.CreateReverseWrapper<LeftHandShiftFunction>(LeftHandShift); ;
            IReverseWrapper<PushRoomToStackFunction> reverseWrapperPushRoomToStackFunction = _hooks.CreateReverseWrapper<PushRoomToStackFunction>(PushRoomToStack);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(PushRoomToStack, out reverseWrapperPushRoomToStackFunction)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(LeftHandShift, out reverseWrapperLeftHandShiftFunction)}");
            _reverseWrapperList.Add(reverseWrapperPushRoomToStackFunction);
            _reverseWrapperList.Add(reverseWrapperLeftHandShiftFunction);

            List<String> functions = _jsonImporter.GetRoomFunctions();

            long address = _utils.SigScan(functions[0], "SetupRoomRam1");
            SetupRoomRAM((int)address, functions[0], RoomPushType.PUSH_ONLY);

            address = _utils.SigScan(functions[1], "SetupRoomRam2");
            SetupRoomRAM((int)address, functions[1], RoomPushType.ADD_COMMANDS_2);

            address = _utils.SigScan(functions[2], "SetupRoomRam3");
            SetupRoomRAM((int)address, functions[2], RoomPushType.ADD_COMMANDS_1);


            address = _utils.SigScan(functions[3], "SkipLoadInGroupD");
            skipFunction((int)address, functions[3]);
        }

        private void skipFunction(int functionAddress, string pattern)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"xor edi, edi");
            instruction_list.Add($"mov [ebp-32], edi");
            instruction_list.Add($"xor ecx, ecx");

            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private void SetupRoomRAM(int functionAddress, string pattern, RoomPushType variance)
        {
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"{_hooks.Utilities.PushCdeclCallerSavedRegisters()}");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"{_hooks.Utilities.PopCdeclCallerSavedRegisters()}");
            instruction_list.Add($"add edi, 0x56");
            switch(variance)
            {
                case RoomPushType.PUSH_ONLY:
                    break;
                case RoomPushType.ADD_COMMANDS_1:
                    instruction_list.Add($"mov ecx, [ebp-0x70]");
                    instruction_list.Add($"mov ecx, [ebp-0x70]");
                    instruction_list.Add($"push eax");
                    instruction_list.Add(_commands[1]);
                    instruction_list.Add($"pop eax");
                    instruction_list.Add($"lea ecx, [ebp-0x5C]");

                    break;
                case RoomPushType.ADD_COMMANDS_2:
                    instruction_list.Add($"mov ecx, [{_utils.AccountForBaseAddress(0x009CAC15)}]");
                    instruction_list.Add($"shl ecx, 24");
                    instruction_list.Add($"shr ecx, 24");
                    instruction_list.Add($"push eax");
                    instruction_list.Add(_commands[1]);
                    instruction_list.Add($"pop eax"); 
                    instruction_list.Add($"lea ecx, [ebp-0x64]");
                    break;
                default:
                    break;
            }
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private int LeftHandShift(int toShift, int shiftAmount)
        {
            return toShift << shiftAmount;
        }

        private void PushRoomToStack(int index, int targetAddress)
        {
            if (index == 0 || index > _dungeonRooms.Count)
            {
                throw new InvalidRoomIndexException(index);
            }
            short counter = 0;
            index--;    //Need to decrement since 0 is never a room possibility (dummy entry)
            _memory.SafeWrite(targetAddress, _dungeonRooms[index].ID);
            counter++;
            _memory.SafeWrite(targetAddress+counter, _dungeonRooms[index].sizeX);
            counter++;
            _memory.SafeWrite(targetAddress+counter, _dungeonRooms[index].sizeY);
            counter++;
            _memory.SafeWrite(targetAddress+counter, (byte)0);
            counter++;
            foreach (List<byte> row in _dungeonRooms[index].connectionPointers)
            {
                foreach (byte value in row)
                {

                    _memory.SafeWrite(targetAddress+counter, value);
                    counter++;
                }
            }
            foreach (List<byte> row in _dungeonRooms[index].revealProperties)
            {
                foreach (byte value in row)
                {

                    _memory.SafeWrite(targetAddress+counter, value);
                    counter++;
                }
            }
            _memory.SafeWrite(targetAddress+counter, _dungeonRooms[index].unknownMasks[0]);
            counter++;
            _memory.SafeWrite(targetAddress+counter, _dungeonRooms[index].unknownMasks[1]);
            counter++;
            foreach (List<byte> row in _dungeonRooms[index].mapRamOutline)
            {
                foreach (byte value in row)
                {

                    _memory.SafeWrite(targetAddress+counter, value);
                    counter++;
                }
            }
            _memory.SafeWrite(targetAddress+counter, (byte)0);
            counter++;

            writeConnectionValues(index, targetAddress, counter);
        }

        private void writeConnectionValues(int index, int targetAddress, int counter)
        {
            foreach (List<int> row in _dungeonRooms[index].connectionValues)
            {
                foreach (int value in row)
                {

                    _memory.SafeWrite(targetAddress+counter, value);
                    counter+=4;
                }
            }
        }

        [Function(new[] { Register.edx, Register.ecx }, Register.edx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int LeftHandShiftFunction(int edx, int ecx);

        [Function(new[] { Register.eax, Register.edi }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void PushRoomToStackFunction(int eax, int edi);
    }
}