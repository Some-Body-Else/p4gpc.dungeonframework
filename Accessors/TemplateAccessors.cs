﻿using Reloaded.Hooks;
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
    public class TemplateAccessor
    {
        private enum templateInstruction
        {
            ROOM_COUNT = 0,
            ROOM_EX_COUNT = 1,
            ROOM_GET = 2
        }
        private enum registerReference
        {
            EAX = 0,    //000
            ECX = 1,    //001
            EDX = 2,    //010
            EBX = 3,    //011
            ESP = 4,    //100
            EBP = 5,    //101
            ESI = 6,    //110
            EDI = 7     //111
        }
        
        private templateInstruction _accessType;
        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private int _functionAddress;

        /// <summary>
        /// These three variables are all meant to represent the data found in the R/M byte in an x86 instruction
        /// </summary>
        private byte mod;
        private registerReference reg_out;
        private registerReference reg_out_src;

        /// <summary>
        /// These three variables are all meant to represent the data found in the SIB byte in an x86 instruction
        /// </summary>
        private byte scale;
        private registerReference reg_in;
        private registerReference reg_base; //This should be irrelevant, but better safe than sorry

        private List<DungeonTemplates>? _dungeonTemplates;
        private List<IAsmHook>? _functionHookList;
        private List<IReverseWrapper>? _reverseWrapperList;
        private List<IReverseWrapper<GetRoomCountFunction>>? _reverseWrapperCountList;
        private List<IReverseWrapper<GetRoomExCountFunction>>? _reverseWrapperCountExList;
        private List<IReverseWrapper<GetRoomFunction>>? _reverseWrapperGetList;
        private IReverseWrapper<GetRoomCountFunction>? _reverseWrapperCount;
        private IReverseWrapper<GetRoomExCountFunction>? _reverseWrapperCountEx;
        private IReverseWrapper<GetRoomFunction>? _reverseWrapperGet;

        public Config _configuration;

        private JsonImporter _jsonImporter;
        private DungeonTemplates _templates;

        private IReverseWrapper _reverseWrapper;
        private List<long> Template_Size_MovZX;

        public TemplateAccessor(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;

            //_jsonImporter will handle all json loading and deserialization
            _jsonImporter = new JsonImporter(_configuration, utils);

            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _dungeonTemplates = _jsonImporter.GetTemplates();

            _reverseWrapperCountList = new List<IReverseWrapper<GetRoomCountFunction>>();
            _reverseWrapperCountExList = new List<IReverseWrapper<GetRoomExCountFunction>>();
            _reverseWrapperGetList = new List<IReverseWrapper<GetRoomFunction>>();

            //Set up garbage here, this is where we sink our hooks in
            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
        }


        public void Initialize()
        {
            Debugger.Launch();
            Template_Size_MovZX = _utils.SigScan_FindCount("0F B6 ?? ?? E4 69 9F 00", "Template_Size_MovZX", 7);
            foreach (int address in Template_Size_MovZX)
            {
                _utils.LogDebug($"Function found at: {address.ToString("X8")}");
                byte R_M_BYTE;
                byte SIB_BYTE;
                _memory.SafeRead((nuint)(address + 2), out R_M_BYTE);
                _utils.LogDebug($"Byte found: {R_M_BYTE}");
                _memory.SafeRead((nuint)(address + 3), out SIB_BYTE);
                _utils.LogDebug($"Byte found: {SIB_BYTE}");
                mod = (byte)(R_M_BYTE >> 6);
                reg_out = (registerReference)((R_M_BYTE >> 3) & 0x7);
                reg_out_src = (registerReference)(R_M_BYTE & 0x7); //4th bit doesn't seem to change mapping, so we treat it as 3
                if (reg_out_src != registerReference.ESP)
                {
                    throw new ToBeNamedExcpetion();
                    //Issue, since we only expect to handle something using the SIB byte
                }
                scale = (byte)(SIB_BYTE >> 6);
                reg_in = (registerReference)((SIB_BYTE >> 3) & 0x7);
                reg_base = (registerReference)(SIB_BYTE & 0x7); //4th bit doesn't seem to change mapping, so we treat it as 3
                if (reg_base != registerReference.EBP)
                {
                    throw new ToBeNamedExcpetion();
                    //Issue, since we only expect to handle something using an address
                }
                _functionAddress = address;
                switch (0x009F69E4)
                {
                    case 0x009F69E4:
                        {
                            _accessType = templateInstruction.ROOM_COUNT;
                            break;
                        }
                    case 0x009F69E5:
                        {
                            _accessType = templateInstruction.ROOM_EX_COUNT;
                            break;
                        }
                    case 0x009F69E6:
                        {
                            _accessType = templateInstruction.ROOM_GET;
                            break;
                        }
                    default:
                        {
                            throw new InvalidTemplateAccessorAddressException(0x7FFFFFFF);
                        }
                }
                switch (_accessType)
                {
                    case (templateInstruction.ROOM_COUNT):
                        {
                            _reverseWrapperCount = _hooks.CreateReverseWrapper<GetRoomCountFunction>(GetRoomCount);
                            break;
                        }
                    case (templateInstruction.ROOM_EX_COUNT):
                        {
                            _reverseWrapperCountEx = _hooks.CreateReverseWrapper<GetRoomExCountFunction>(GetRoomExCount);
                            break;
                        }
                    case (templateInstruction.ROOM_GET):
                        {
                            _reverseWrapperGet = _hooks.CreateReverseWrapper<GetRoomFunction>(GetRoom);
                            break;
                        }
                    default:
                        {
                            throw new ToBeNamedExcpetion();
                            break;
                        }
                }
                SetupAsm_MovZX();
            }
        }

        private void SetupAsm_MovZX()
        {
            List<string> instruction_list = new List<string>();
            bool eax_out = false;
            bool ecx_out = false;
            bool edx_out = false;
            instruction_list.Add($"use32");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
            //Technically there's two other forms the mod can have (1 and 3)
            //however they don't seem to be used for our particular functions
            if (mod == 0)
            {
                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov eax, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            instruction_list.Add($"mov eax, edx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            instruction_list.Add($"mov eax, ebx");
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov eax, esp");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov eax, ebp");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov eax, esi");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov eax, edi");
                            break;
                        }
                    default:
                        {
                            throw new ToBeNamedExcpetion();
                        }
                }

                if (_accessType == templateInstruction.ROOM_COUNT)
                {
                    instruction_list.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRoomCount, out _reverseWrapperCount)}");
                }
                else if (_accessType == templateInstruction.ROOM_EX_COUNT)
                {
                    instruction_list.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRoomExCount, out _reverseWrapperCountEx)}");
                }
                else
                {
                    throw new ToBeNamedExcpetion();
                }

                switch (reg_out)
                {
                    case registerReference.EAX:
                        {
                            eax_out = true;
                            instruction_list.Add($"mov eax, ecx");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            ecx_out = true;
                            break;
                        }
                    case registerReference.EDX:
                        {
                            edx_out = true;
                            instruction_list.Add($"mov edx, ecx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            instruction_list.Add($"mov ebx, ecx");
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov esp, ecx");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov ebp, ecx");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov esi, ecx");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov edi, ecx");
                            break;
                        }
                }

                if (eax_out)
                {
                    instruction_list.Add($"pop edx");
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"add esp, 4");
                }
                else if (ecx_out)
                {
                    instruction_list.Add($"pop edx");
                    instruction_list.Add($"pop eax");
                    instruction_list.Add($"pop eax");
                }
                else if (edx_out)
                {
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"pop eax");
                }
                else
                {
                    instruction_list.Add($"pop edx");
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"pop eax");
                }
                //(index register's contents * scale) + base_address
                //Accesses one of the room size values,
            }
            else if (mod == 2)
            {
                //In the off chance that reg_in is EDX and reg_base is EAX, we would lose information with our algorithm as is
                //We'll detect if that's the case and flip it if necessary
                bool flipped = false;
                //(index register's contents * scale) + baseregisterContent+ base_address
                //_accessor.AccessTemplate(reg_in, reg_base);
                //Accesses the rooms individually, so we need another register

                //Actual 'difficult' part here, once again need more switch/case statements

                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov eax, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            if (reg_base == registerReference.EBX)
                            {
                                flipped = true;
                                break;
                            }
                            instruction_list.Add($"mov eax, edx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            instruction_list.Add($"mov eax, ebx");
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov eax, esp");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov eax, ebp");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov eax, esi");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov eax, edi");
                            break;
                        }
                    default:
                        {
                            throw new ToBeNamedExcpetion();
                        }
                }

                switch (reg_base)
                {
                    case registerReference.EAX:
                        {
                            if (flipped == true)
                            {
                                break;
                            }
                            instruction_list.Add($"mov edx, eax");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov edx, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            break;
                        }
                    case registerReference.EBX:
                        {

                            instruction_list.Add($"mov edx, ebx");
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov edx, esp");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov edx, ebp");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov edx, esi");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov edx, edi");
                            break;
                        }
                    default:
                        {
                            throw new ToBeNamedExcpetion();
                        }
                }
                if (flipped)
                {
                    instruction_list.Add($"push ecx");
                    instruction_list.Add($"mov ecx, edx");
                    instruction_list.Add($"mov edx, eax");
                    instruction_list.Add($"mov eax, ecx");
                    instruction_list.Add($"pop ecx");
                }


                if (_accessType != templateInstruction.ROOM_GET)
                {
                    throw new ToBeNamedExcpetion();
                }
                else
                {
                    instruction_list.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRoom, out _reverseWrapperGet)}");
                }
                switch (reg_out)
                {
                    case registerReference.EAX:
                        {
                            eax_out = true;
                            instruction_list.Add($"mov eax, ecx");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            ecx_out = true;
                            break;
                        }
                    case registerReference.EDX:
                        {
                            edx_out = true;
                            instruction_list.Add($"mov edx, ecx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            instruction_list.Add($"mov ebx, ecx");
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov esp, ecx");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov ebp, ecx");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov esi, ecx");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov edi, ecx");
                            break;
                        }
                }
                if (eax_out)
                {
                    instruction_list.Add($"pop edx");
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"add esp, 4");
                }
                else if (ecx_out)
                {
                    instruction_list.Add($"pop edx");
                    instruction_list.Add($"pop eax");
                    instruction_list.Add($"pop eax");
                }
                else if (edx_out)
                {
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"pop eax");
                }
                else
                {
                    instruction_list.Add($"pop edx");
                    instruction_list.Add($"pop ecx");
                    instruction_list.Add($"pop eax");
                }

            }
            else
            {
                throw new ToBeNamedExcpetion();
            }
            //Do NOT want a return, since Reloaded-II's jump-based system means that there's no address to return to.
            //instruction_list.Add($"ret");
            switch (_accessType)
            {
                case (templateInstruction.ROOM_COUNT):
                    {
                        _reverseWrapperCountList.Add(_reverseWrapperCount);
                        break;
                    }
                case (templateInstruction.ROOM_EX_COUNT):
                    {
                        _reverseWrapperCountExList.Add(_reverseWrapperCountEx);
                        break;
                    }
                case (templateInstruction.ROOM_GET):
                    {
                        _reverseWrapperGetList.Add(_reverseWrapperGet);
                        break;
                    }
                default:
                    {
                        throw new ToBeNamedExcpetion();
                        break;
                    }
            }
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), _functionAddress, AsmHookBehaviour.DoNotExecuteOriginal).Activate());
            //return instruction_list.ToArray();
        }

        private int GetRoomCount(int ebx)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            ebx /= 3;
            return _dungeonTemplates[ebx].roomCount;
        }

        private int GetRoomExCount(int ebx)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            //We will presumably change this once we have the foundations for this mod a bit more settled
            ebx /= 3;
            return _dungeonTemplates[ebx].roomExCount;
        }

        private int GetRoom(int ebx, int edx)
        {
            if (edx >= _dungeonTemplates[ebx].rooms.Count || edx <= 0)
            {
                throw new ArgumentOutOfRangeException("edx", $"Attempting to access room index {edx} while there exist {_dungeonTemplates[ebx].rooms.Count} rooms.");
            }
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            //Room Index is the same value, however
            ebx /= 3;
            return _dungeonTemplates[ebx].rooms[edx];
        }

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomCountFunction(int ebx);

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomExCountFunction(int ebx);

        [Function(new[] { Register.eax, Register.edx }, Register.ecx, StackCleanup.Callee)]
        //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomFunction(int ebx, int edx);
    }
}