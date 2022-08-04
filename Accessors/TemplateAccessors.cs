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

using p4gpc.dungeonloader.Exceptions;
using p4gpc.dungeonloader.JsonClasses;

namespace p4gpc.dungeonloader.Accessors
{
    public enum templateInstruction
    {
        ROOM_COUNT = 0,
        ROOM_EX_COUNT = 1,
        ROOM_GET = 2
    }
    enum registerReference
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
    public class MoveZeroExtended
    {

        [Function(Register.ebx, Register.ebx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomCountFunction(int ebx);

        [Function(Register.ebx, Register.ebx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomExCountFunction(int ebx);

        [Function(new[] { Register.ebx, Register.edx }, Register.ebx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomFunction(int ebx, int edx);

        private templateInstruction _accessType;
        private IReloadedHooks _hooks;
        private Utilities _utils;
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

        private List<DungeonTemplates> _dungeonTemplates;
        private IAsmHook? _functionHook;
        private IReverseWrapper<GetRoomCountFunction>? _reverseWrapperCount;
        private IReverseWrapper<GetRoomExCountFunction>? _reverseWrapperCountEx;
        private IReverseWrapper<GetRoomFunction>? _reverseWrapperGet;

        public MoveZeroExtended(byte R_M_BYTE, byte SIB_BYTE, List<DungeonTemplates> dungeonTemplates, int functionAddress, int templateAddress, IReloadedHooks hooks, Utilities utils)
        {
            _utils = utils;
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
            _functionAddress = functionAddress;
            _dungeonTemplates = dungeonTemplates;
            switch (templateAddress)
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
                        throw new InvalidTemplateAccessorAddressException(templateAddress);
                    }
            }
            _hooks = hooks;

            _reverseWrapperCount = _hooks.CreateReverseWrapper<GetRoomCountFunction>(GetRoomExCount);
            _reverseWrapperCountEx = _hooks.CreateReverseWrapper<GetRoomExCountFunction>(GetRoomExCount);
            _reverseWrapperGet = _hooks.CreateReverseWrapper<GetRoomFunction>(GetRoom);
            Initialize();
        }

        private int GetRoomCount(int templateIndex)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            _utils.Log("Got to GetRoomCount function");
            templateIndex /= 3;
            return _dungeonTemplates[templateIndex].roomCount;
        }

        private int GetRoomExCount(int templateIndex)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            //We will presumably change this once we have the foundations for this mod a bit more settled
            templateIndex /= 3;
            return _dungeonTemplates[templateIndex].roomExCount;
        }

        private int GetRoom(int templateIndex, int roomIndex)
        {
            if (roomIndex >= _dungeonTemplates.ElementAt(templateIndex).rooms.Count || roomIndex <= 0)
            {
                throw new ArgumentOutOfRangeException("roomIndex", $"Attempting to access room index {roomIndex} while there exist {_dungeonTemplates.ElementAt(templateIndex).rooms.Count} rooms.");
            }
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            templateIndex /= 3;
            //Room Index is the same value, however
            return _dungeonTemplates[templateIndex].rooms[roomIndex];
        }

        public void Initialize()
        {
            List<string> instruction_list = new List<string>();
            bool ebx_out = false;
            bool edx_out = false;
            instruction_list.Add($"use32");
            if (reg_out != registerReference.EBX)
            {
                instruction_list.Add($"push ebx");
            }
            else
            {
                ebx_out = true;
            }
            //Technically there's two other forms the mod can have (1 and 3)
            //however they don't seem to be used for our particular functions
            if (mod == 0)
            {
                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
                            instruction_list.Add($"mov ebx, eax");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov ebx, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            instruction_list.Add($"mov ebx, edx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov ebx, esp");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov ebx, ebp");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov ebx, esi");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov ebx, edi");
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
                            instruction_list.Add($"mov eax, ebx");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov ecx, ebx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            instruction_list.Add($"mov edx, ebx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov esp, ebx");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov ebp, ebx");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov esi, ebx");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov edi, ebx");
                            break;
                        }
                }
                if (!ebx_out)
                {
                    instruction_list.Add($"pop ebx");
                }

                //(index register's contents * scale) + base_address
                //Accesses one of the room size values,
            }
            else if (mod == 2)
            {
                //In the off chance that reg_in is EDX and reg_base is EBX, we would lose information with our algorithm as is
                //We'll detect if that's the case and flip it if necessary
                bool flipped = false;
                //(index register's contents * scale) + baseregisterContent+ base_address
                //_accessor.AccessTemplate(reg_in, reg_base);
                //Accesses the rooms individually, so we need another register
                if (reg_out != registerReference.EDX)
                {
                    instruction_list.Add($"push edx");
                }
                else
                {
                    edx_out = true;
                }
                //Actual 'difficult' part here, once again need more switch/case statements

                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
                            instruction_list.Add($"mov ebx, eax");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov ebx, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            if (reg_base == registerReference.EBX)
                            {
                                flipped = true;
                                break;
                            }
                            instruction_list.Add($"mov ebx, edx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov ebx, esp");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov ebx, ebp");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov ebx, esi");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov ebx, edi");
                            break;
                        }
                    default:
                        {
                            throw new ToBeNamedExcpetion();
                        }
                }

                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
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
                            if (flipped == true)
                            {
                                break;
                            }
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
                    instruction_list.Add($"mov edx, ebx");
                    instruction_list.Add($"mov ebx, ecx");
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
                            instruction_list.Add($"mov eax, ebx");
                            break;
                        }
                    case registerReference.ECX:
                        {
                            instruction_list.Add($"mov ecx, ebx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            instruction_list.Add($"mov edx, ebx");
                            break;
                        }
                    case registerReference.EBX:
                        {
                            break;
                        }
                    case registerReference.ESP:
                        {
                            instruction_list.Add($"mov esp, ebx");
                            break;
                        }
                    case registerReference.EBP:
                        {
                            instruction_list.Add($"mov ebp, ebx");
                            break;
                        }
                    case registerReference.ESI:
                        {
                            instruction_list.Add($"mov esi, ebx");
                            break;
                        }
                    case registerReference.EDI:
                        {
                            instruction_list.Add($"mov edi, ebx");
                            break;
                        }
                }

                if (!ebx_out)
                {
                    instruction_list.Add($"pop ebx");
                }
                if (!edx_out)
                {
                    instruction_list.Add($"pop edx");
                }

            }
            else
            {
                throw new ToBeNamedExcpetion();
            }

            _functionHook = _hooks.CreateAsmHook(instruction_list.ToArray(), _functionAddress, AsmHookBehaviour.DoNotExecuteOriginal).Activate();
            //return instruction_list.ToArray();
        }


    }
}