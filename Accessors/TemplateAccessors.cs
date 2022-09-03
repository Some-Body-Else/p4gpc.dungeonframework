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
    public class TemplateAccessors
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
        private enum instructionType
        {
            MOV,
            MOVZX,
            MOVSX,
            MOVSX_LOWER,
            CMP,
            CMP_CONST
        }
        
        private int _functionAddress;
        private byte mod;
        private registerReference reg_out;
        private registerReference reg_out_src;
        private byte scale;
        private registerReference reg_in;
        private registerReference reg_base;
        private byte constInput;

        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private List<IReverseWrapper> _reverseWrapperList;
        private List<IAsmHook> _functionHookList;
        private List<DungeonTemplates>? _dungeonTemplates;
        private List<String> _commands;

        public TemplateAccessors(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _dungeonTemplates = _jsonImporter.GetTemplates();
            _commands = new List<String>();

            //Debugger.Launch();

            //Set up garbage here, this is where we sink our hooks in
            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
            _utils.Log("Template-adjacent hooks established.");
        }


        public void Initialize()
        {
            byte idByte;
            byte idByte2 = 0;
            byte idByte3 = 0;
            byte R_M_BYTE;
            byte SIB_BYTE;
            int accessedAddress;
            bool twoByteID = false;
            bool threeByteID = false;
            templateInstruction accessType;
            List<String> functions = _jsonImporter.GetTemplateFunctions();


            List<long> foundAddresses;
            instructionType currentInstruction;

            IReverseWrapper<GetRoomCountFunction> reverseWrapperGetRoomCount = _hooks.CreateReverseWrapper<GetRoomCountFunction>(GetRoomCount);
            IReverseWrapper<GetRoomExCountFunction> reverseWrapperGetRoomExCount = _hooks.CreateReverseWrapper<GetRoomExCountFunction>(GetRoomExCount);
            IReverseWrapper<GetRoomFunction> reverseWrapperGetRoom = _hooks.CreateReverseWrapper<GetRoomFunction>(GetRoom);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRoomCount, out reverseWrapperGetRoomCount)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRoomExCount, out reverseWrapperGetRoomExCount)}");
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(GetRoom, out reverseWrapperGetRoom)}");
            _reverseWrapperList.Add(reverseWrapperGetRoomCount);
            _reverseWrapperList.Add(reverseWrapperGetRoomExCount);
            _reverseWrapperList.Add(reverseWrapperGetRoom);
            foreach (string function in functions)
            {

                foundAddresses = _utils.SigScan_FindAll(function, "TemplateFunction");
                foreach (int address in foundAddresses)
                {
                    twoByteID = false;
                    threeByteID = false;
                    _utils.LogDebug($"Function found at: {address.ToString("X8")}");
                    _memory.SafeRead((nuint)(address), out idByte);
                    if (idByte == 0x0F || idByte == 0x40)
                    {
                        twoByteID = true;
                        _memory.SafeRead((nuint)(address + 1), out idByte2);
                    }
                    else if (idByte == 0x66)
                    {
                        threeByteID = true;
                        _memory.SafeRead((nuint)(address + 1), out idByte2);
                        _memory.SafeRead((nuint)(address + 2), out idByte3);
                    }
                    if (twoByteID)
                    {
                        _memory.SafeRead((nuint)(address + 4), out accessedAddress);
                        _memory.SafeRead((nuint)(address + 2), out R_M_BYTE);
                        _memory.SafeRead((nuint)(address + 3), out SIB_BYTE);
                        if (idByte == 0x0F)
                        {
                            if (idByte2 == 0xBE)
                            {

                                currentInstruction = instructionType.MOVSX;
                            }
                            else if (idByte2 == 0xB6)
                            {
                                currentInstruction = instructionType.MOVZX;

                            }
                            else
                            {
                                throw new InvalidAsmInstructionTypeException(_functionAddress);
                            }
                        }
                        else
                        {
                            throw new InvalidAsmInstructionTypeException(_functionAddress);
                        }
                    }
                    else if (threeByteID)
                    {
                        _memory.SafeRead((nuint)(address + 5), out accessedAddress);
                        _memory.SafeRead((nuint)(address + 3), out R_M_BYTE);
                        _memory.SafeRead((nuint)(address + 4), out SIB_BYTE);
                        if (idByte == 0x66)
                        {
                            if (idByte2 == 0x0F)
                            {
                                if (idByte3 == 0xBE)
                                {
                                    currentInstruction = instructionType.MOVSX_LOWER;
                                }
                                else
                                {
                                    throw new InvalidAsmInstructionTypeException(_functionAddress);
                                }
                            }
                            else
                            {
                                throw new InvalidAsmInstructionTypeException(_functionAddress);
                            }
                        }
                        else
                        {
                            throw new InvalidAsmInstructionTypeException(_functionAddress);
                        }
                    }
                    else
                    {
                        if (idByte == 0x38)
                        {

                            currentInstruction = instructionType.CMP;
                        }
                        else if (idByte == 0x80)
                        {
                            currentInstruction = instructionType.CMP_CONST;
                            _memory.SafeRead((nuint)(address + 7), out constInput);

                        }
                        else if (idByte == 0x8A)
                        {
                            currentInstruction = instructionType.MOV;
                        }
                        else
                        {
                            throw new InvalidAsmInstructionTypeException(_functionAddress);
                        }
                        _memory.SafeRead((nuint)(address + 3), out accessedAddress);
                        _memory.SafeRead((nuint)(address + 1), out R_M_BYTE);
                        _memory.SafeRead((nuint)(address + 2), out SIB_BYTE);
                    }
                    mod = (byte)(R_M_BYTE >> 6);
                    reg_out = (registerReference)((R_M_BYTE >> 3) & 0x7);
                    reg_out_src = (registerReference)(R_M_BYTE & 0x7); 
                    scale = (byte)(SIB_BYTE >> 6);
                    reg_in = (registerReference)((SIB_BYTE >> 3) & 0x7);
                    reg_base = (registerReference)(SIB_BYTE & 0x7);
                    _functionAddress = address;
                    switch (accessedAddress)
                    {
                        case 0x009F69E4:
                            {
                                accessType = templateInstruction.ROOM_COUNT;
                                break;
                            }
                        case 0x009F69E5:
                            {
                                accessType = templateInstruction.ROOM_EX_COUNT;
                                break;
                            }
                        case 0x009F69E6:
                            {
                                accessType = templateInstruction.ROOM_GET;
                                break;
                            }
                        default:
                            {
                                throw new InvalidTemplateAccessorAddressException(0x7FFFFFFF);
                            }
                    }
                    switch (currentInstruction)
                    {
                        case instructionType.MOVZX:
                        case instructionType.MOVSX:
                        case instructionType.MOVSX_LOWER:
                        case instructionType.MOV:
                            SetupAsm_Mov(accessType);
                            break;
                        case instructionType.CMP:
                        case instructionType.CMP_CONST:
                            SetupAsm_Cmp(currentInstruction, accessType);
                            break;
                        default:
                            throw new InvalidAsmInstructionTypeException(_functionAddress);
                    };
                }

            }
        }
        private void SetupAsm_Mov(templateInstruction accessType)
        {
            List<string> instruction_list = new List<string>();
            bool eax_out = false;
            bool ecx_out = false;
            bool edx_out = false;
            instruction_list.Add($"use32");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"push edx");
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
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                        }
                }

                if (accessType == templateInstruction.ROOM_COUNT)
                {
                    instruction_list.Add(_commands[0]);
                }
                else if (accessType == templateInstruction.ROOM_EX_COUNT)
                {
                    instruction_list.Add(_commands[1]);
                }
                else
                {
                    throw new InvalidAsmInstructionModAccessCombinationException(_functionAddress);
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
                    default:
                        {
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
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
            else if (mod == 2)
            {
                //In the off chance that reg_in is EDX and reg_base is EAX, we would lose information with our algorithm as is
                //We'll detect if that's the case and flip it if necessary
                bool flipAD = false;
                bool flipAC = false;

                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
                            break;
                        }
                    case registerReference.ECX:
                        {
                            if (reg_base == registerReference.EAX)
                            {
                                flipAC = true;
                                break;
                            }
                            instruction_list.Add($"mov eax, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            if (reg_base == registerReference.EAX)
                            {
                                flipAD = true;
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
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                        }
                }

                switch (reg_base)
                {
                    case registerReference.EAX:
                        {
                            if (flipAD || flipAC)
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
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                        }
                }
                if (flipAD)
                {
                    instruction_list.Add($"push ecx");
                    instruction_list.Add($"mov ecx, edx");
                    instruction_list.Add($"mov edx, eax");
                    instruction_list.Add($"mov eax, ecx");
                    instruction_list.Add($"pop ecx");
                }
                if (flipAC)
                {
                    instruction_list.Add($"push eax");
                    instruction_list.Add($"mov eax, ecx");
                    instruction_list.Add($"pop edx");
                }


                if (accessType == templateInstruction.ROOM_GET)

                {
                    instruction_list.Add(_commands[2]);

                }
                else if (accessType == templateInstruction.ROOM_EX_COUNT)
                {
                    //There's a one-off instruction that uses the address associated
                    //with the ex room count that for the sake of getting a value from
                    //the template table. This will account for it.

                    instruction_list.Add($"sub edx, 1");
                    instruction_list.Add(_commands[2]);
                    accessType = templateInstruction.ROOM_GET;
                }
                else
                {
                    throw new InvalidAsmInstructionModAccessCombinationException(_functionAddress);
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
                    default:
                        {
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
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
                throw new InvalidAsmInstructionModValueException(_functionAddress);
            }
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), _functionAddress, AsmHookBehaviour.DoNotExecuteOriginal).Activate());
        }

        private void SetupAsm_Cmp(instructionType currentInstruction, templateInstruction accessType)
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
            //Will be added if the need arises
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
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                        }
                }

                if (accessType == templateInstruction.ROOM_COUNT)
                {
                    instruction_list.Add(_commands[0]);
                }
                else if (accessType == templateInstruction.ROOM_EX_COUNT)
                {
                    instruction_list.Add(_commands[1]);
                }
                else
                {
                    throw new InvalidAsmInstructionModAccessCombinationException(_functionAddress);
                }

                if (currentInstruction == instructionType.CMP_CONST)
                {
                    instruction_list.Add($"cmp ecx, {constInput}");
                }
                else
                {
                    switch (reg_out)
                    {
                        case registerReference.EAX:
                            {
                                eax_out = true;
                                instruction_list.Add($"add esp, 8");
                                instruction_list.Add($"pop eax");
                                instruction_list.Add($"sub esp, 8");
                                instruction_list.Add($"pop edx");

                                instruction_list.Add($"push eax");
                                instruction_list.Add($"push edx");
                                instruction_list.Add($"sub esp, 4");

                                instruction_list.Add($"pop edx");
                                instruction_list.Add($"pop eax");

                                instruction_list.Add($"cmp eax, ecx");

                                break;
                            }
                        case registerReference.ECX:
                            {
                                ecx_out = true;
                                instruction_list.Add($"pop edx");
                                instruction_list.Add($"mov eax, ecx");
                                instruction_list.Add($"pop ecx");
                                instruction_list.Add($"cmp eax, ecx");

                                break;
                            }
                        case registerReference.EDX:
                            {
                                edx_out = true;
                                instruction_list.Add($"cmp edx, ecx");
                                break;
                            }
                        case registerReference.EBX:
                            {
                                instruction_list.Add($"cmp ebx, ecx");
                                break;
                            }
                        case registerReference.ESP:
                            {
                                instruction_list.Add($"cmp esp, ecx");
                                break;
                            }
                        case registerReference.EBP:
                            {
                                instruction_list.Add($"cmp ebp, ecx");
                                break;
                            }
                        case registerReference.ESI:
                            {
                                instruction_list.Add($"cmp esi, ecx");
                                break;
                            }
                        case registerReference.EDI:
                            {
                                instruction_list.Add($"cmp edi, ecx");
                                break;
                            }
                        default:
                            {
                                throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                            }
                    }
                }
                if (eax_out)
                {
                    instruction_list.Add($"pop ecx");
                }
                else if (ecx_out)
                {
                    instruction_list.Add($"pop eax");
                }
                else if (edx_out)
                {
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
            else if (mod == 2)
            {
                bool flipAD = false;
                bool flipAC = false;
                switch (reg_in)
                {
                    case registerReference.EAX:
                        {
                            break;
                        }
                    case registerReference.ECX:
                        {
                            if (reg_base == registerReference.EAX)
                            {
                                flipAC = true;
                                break;
                            }
                            instruction_list.Add($"mov eax, ecx");
                            break;
                        }
                    case registerReference.EDX:
                        {
                            if (reg_base == registerReference.EAX)
                            {
                                flipAD = true;
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
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                        }
                }

                switch (reg_base)
                {
                    case registerReference.EAX:
                        {
                            if (flipAD || flipAC)
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
                            throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                        }
                }
                if (flipAD)
                {
                    instruction_list.Add($"push ecx");
                    instruction_list.Add($"mov ecx, edx");
                    instruction_list.Add($"mov edx, eax");
                    instruction_list.Add($"mov eax, ecx");
                    instruction_list.Add($"pop ecx");
                }
                if (flipAC)
                {
                    instruction_list.Add($"push eax");
                    instruction_list.Add($"mov eax, ecx");
                    instruction_list.Add($"pop edx");
                }


                if (accessType == templateInstruction.ROOM_GET)

                {
                    instruction_list.Add(_commands[2]);

                }
                else if (accessType == templateInstruction.ROOM_EX_COUNT)
                {
                    //There's a one-off instruction that uses the address associated
                    //with the ex room count that for the sake of getting a value from
                    //the template table. This will account for it.

                    instruction_list.Add($"sub edx, 1");
                    instruction_list.Add(_commands[2]);
                    accessType = templateInstruction.ROOM_GET;
                }
                else
                {
                    throw new InvalidAsmInstructionModAccessCombinationException(_functionAddress);
                }
                if (currentInstruction == instructionType.CMP_CONST)
                {
                    instruction_list.Add($"cmp ecx, {constInput}");
                }
                else
                {
                    switch (reg_out)
                    {
                        case registerReference.EAX:
                            {
                                eax_out = true;
                                instruction_list.Add($"add esp, 8");
                                instruction_list.Add($"pop eax");
                                instruction_list.Add($"sub esp, 8");
                                instruction_list.Add($"pop edx");

                                instruction_list.Add($"push eax");
                                instruction_list.Add($"push edx");
                                instruction_list.Add($"sub esp, 4");

                                instruction_list.Add($"pop edx");
                                instruction_list.Add($"pop eax");

                                instruction_list.Add($"cmp eax, ecx");

                                break;
                            }
                        case registerReference.ECX:
                            {
                                ecx_out = true;
                                instruction_list.Add($"pop edx");
                                instruction_list.Add($"mov eax, ecx");
                                instruction_list.Add($"pop ecx");
                                instruction_list.Add($"cmp eax, ecx");

                                break;
                            }
                        case registerReference.EDX:
                            {
                                edx_out = true;
                                instruction_list.Add($"cmp edx, ecx");
                                break;
                            }
                        case registerReference.EBX:
                            {
                                instruction_list.Add($"cmp ebx, ecx");
                                break;
                            }
                        case registerReference.ESP:
                            {
                                instruction_list.Add($"cmp esp, ecx");
                                break;
                            }
                        case registerReference.EBP:
                            {
                                instruction_list.Add($"cmp ebp, ecx");
                                break;
                            }
                        case registerReference.ESI:
                            {
                                instruction_list.Add($"cmp esi, ecx");
                                break;
                            }
                        case registerReference.EDI:
                            {
                                instruction_list.Add($"cmp edi, ecx");
                                break;
                            }
                        default:
                            {
                                throw new InvalidAsmInstructionRegisterReferenceException(_functionAddress);
                            }
                    }
                }
                if (eax_out)
                {
                    instruction_list.Add($"pop ecx");
                }
                else if (ecx_out)
                {
                    instruction_list.Add($"pop eax");
                }
                else if (edx_out)
                {
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
                throw new InvalidAsmInstructionModValueException(_functionAddress);
            }
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), _functionAddress, AsmHookBehaviour.DoNotExecuteOriginal).Activate());
        }


        private int GetRoomCount(int eax)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            eax /= 3;
            return _dungeonTemplates[eax].roomCount;
        }

        private int GetRoomExCount(int eax)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            //We will presumably change this once we have the foundations for this mod a bit more settled
            eax /= 3;
            return _dungeonTemplates[eax].roomExCount;
        }

        private int GetRoom(int eax, int edx)
        {
            //Everything that accesses the template  has the index multiplied by 3 because each template is 12 bytes long.
            //The index is multiplied by 4 to actually access the template.
            //Room Index is the same value, however
            eax /= 3;
            if (edx >= _dungeonTemplates[eax].rooms.Count || edx < 0)
            {
                throw new ArgumentOutOfRangeException("edx", $"Attempting to access room index {edx} while there exist {_dungeonTemplates[eax].rooms.Count} rooms.");
            }
            return _dungeonTemplates[eax].rooms[edx];
        }

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomCountFunction(int eax);

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomExCountFunction(int eax);

        [Function(new[] { Register.eax, Register.edx }, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRoomFunction(int eax, int edx);
    }
}