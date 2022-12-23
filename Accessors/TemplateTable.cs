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

namespace p4gpc.dungeonloader.Accessors
{
    public class TemplateTable : Accessor
    {

        /*
        To do:
            -Handle the internal list that tells the game which dungeons have which templates
         */

        private List<DungeonTemplates> _templates;
        private nuint _newTemplateTable;

        public TemplateTable(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)// : base(hooks, utils, memory, config, jsonImporter)
        {
            _templates = _jsonImporter.GetTemplates();;
            executeAccessor(hooks, utils, memory, config, jsonImporter);
            _utils.LogDebug("Templates hooks established.");
        }

        protected override void Initialize()
        {
            Debugger.Launch();
            List<long> functions;
            String address_str_new;
            String address_str_old;
            String search_string = "0F ?? ?? ?? ";
            long address;
            int totalTemplateTableSize = 0;
            
            long _templateTable = _utils.SigScan("08 09 01 02 03 05 06 07 09 0A 04 00 09 0A 01 02 03 05 07 06 08 0B 0C 04 08 09 01 02 03 05 06 08 0D 0E 04 00", "TemplateTable");
            _utils.LogDebug($"Original template table address: {_templateTable.ToString("X8")}", 1);
            _templateTable = _utils.StripBaseAddress(_templateTable);
            _utils.LogDebug($"Accounting for base address: {_templateTable.ToString("X8")}", 1);


            foreach (DungeonTemplates template in _templates)
            {
                totalTemplateTableSize += (template.roomExCount + 2);
            }


            _newTemplateTable = _memory.Allocate(totalTemplateTableSize);
            _utils.LogDebug($"New template table address: {_newTemplateTable.ToString("X8")}", 1);
            _utils.LogDebug($"New template table size: {totalTemplateTableSize.ToString("X8")} bytes", 1);

            address = _utils.StripBaseAddress((long)_newTemplateTable);
            if (address > Int32.MaxValue || Int32.MinValue > address)
            {
                throw new ToBeNamedException(_utils);
            }

            totalTemplateTableSize = 0;
            foreach (DungeonTemplates template in _templates)
            {
                _memory.SafeWrite(_newTemplateTable, template.roomCount);
                _memory.SafeWrite(_newTemplateTable+1, template.roomExCount);
                totalTemplateTableSize += 2;
                foreach (int room in template.rooms)
                {
                    _memory.SafeWrite((_newTemplateTable + (nuint)totalTemplateTableSize), template.roomExCount);
                    totalTemplateTableSize++;
                }
            }
            _utils.LogDebug($"New template table initialized!");


            for (int i = 0; i < 3; i++)
            {

                address_str_old = (_templateTable+i).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                _utils.LogDebug($"Old template table address for search: {address_str_old} bytes", 2);
                
                functions = _utils.SigScan_FindAll(search_string + address_str_old, "TemplateTable Move/Compare Opcodes");
                foreach (long function in functions)
                {
                    _memory.SafeWriteRaw((nuint)function+4, BitConverter.GetBytes(address+i));
                }
            }
            _utils.LogDebug($"First search target replaced",2);

            search_string = "?? 80 BC ?? ";
            for (int i = 0; i < 3; i++)
            {

                address_str_old = (_templateTable+i).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                _utils.LogDebug($"Old template table address for search: {address_str_old} bytes", 2);

                functions = _utils.SigScan_FindAll(search_string + address_str_old, "TemplateTable Move/Compare Opcodes");
                foreach (long function in functions)
                {
                    _memory.SafeWriteRaw((nuint)function+4, BitConverter.GetBytes(address+i));
                }
            }
            _utils.LogDebug($"Second search target replaced", 2);

            search_string = "??  8D ?? ";
            for (int i = 0; i < 3; i++)
            {

                address_str_old = (_templateTable+i).ToString("X8");
                address_str_old = address_str_old.Substring(6, 2) + " " + address_str_old.Substring(4, 2) + " " + address_str_old.Substring(2, 2) + " " + address_str_old.Substring(0, 2);
                _utils.LogDebug($"Old template table address for search: {address_str_old} bytes", 2);

                functions = _utils.SigScan_FindAll(search_string + address_str_old, "TemplateTable Move/Compare Opcodes");
                foreach (long function in functions)
                {
                    _memory.SafeWriteRaw((nuint)function+3, BitConverter.GetBytes(address+i));
                }
            }
            _utils.LogDebug($"Third search target replaced", 2);
            /*
            IReverseWrapper<LogDebugASMFunction> reverseWrapperLogDebugASM = _hooks.CreateReverseWrapper<LogDebugASMFunction>(LogDebugASM);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(LogDebugASM, out reverseWrapperLogDebugASM)}");
            _reverseWrapperList.Add(reverseWrapperLogDebugASM);
            */

        }

        /*
        private void Minimap_Adj_3(int functionAddress, string pattern)
        {
            // Code to replace:
            // ?? 0F ?? ?? ?? ?? B3 A7 00
            //nuint tableAddress = createMinimapAdjustTable2();
            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"mov eax, [esp+0x33]");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"and eax, 0xFF");
            instruction_list.Add($"sub eax, 1");
            instruction_list.Add($"shl eax, 0x2");
            instruction_list.Add($"add ebx, eax");
            instruction_list.Add($"mov eax, [ebx]");
            instruction_list.Add($"mov [esp+0xC], eax");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"ret");
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private nuint createMinimapAdjustTable3()
        {
            nuint memoryAddress;
            int address1 = (int)_utils.SigScan("C7 46 24 00 00 00 3D C7 46 28 00 00 00 3D", "Adjust3_Default");
            int address2 = (int)_utils.SigScan("C7 46 24 00 00 80 3C C7 46 28 00 00 80 3C C7 46 1C 00 00 98 3E C7 46 20 00 00 14 3F 38 CA", "Adjust3_1");
            int address3 = (int)_utils.SigScan("C7 46 20 00 00 14 3F C7 46 1C 00 00 14 3F C7 46 30 00 00 98 41", "Adjust3_2");
            int address4 = (int)_utils.SigScan("C7 46 24 00 00 80 3C C7 46 28 00 00 80 3C C7 46 20 00 00 14 3F", "Adjust3_3");
            int address5 = (int)_utils.SigScan("C7 46 24 00 00 98 3E C7 46 28 00 00 80 3C C7 46 1C 00 00 60 3F", "Adjust3_4");
            int address6 = (int)_utils.SigScan("C7 46 28 00 00 80 3C C7 46 24 00 00 80 3C C7 46 1C 00 00 98 3E", "Adjust3_5");
            int address7 = (int)_utils.SigScan("C7 46 1C 00 00 5C 3F C7 46 20 00 00 5C 3F C7 46 30 00 00 14 42", "Adjust3_6");
            memoryAddress = _memory.Allocate(4 * _minimap_image_count);
            int counter = 0;
            //
            foreach (DungeonMinimap tile in _minimap)
            {
                switch (tile.uVarsSingle[2])
                {
                    case 0:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address1);
                        break;
                    case 1:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address2);
                        break;
                    case 2:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address3);
                        break;
                    case 3:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address4);
                        break;
                    case 4:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address5);
                        break;
                    case 5:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address6);
                        break;
                    case 6:
                        _memory.SafeWrite((memoryAddress + (nuint)counter), address7);
                        break;
                    default:
                        throw new ToBeNamedException(_utils);
                }
                counter+=4;
                if (tile.multipleNames)
                {
                    foreach (List<byte> roomchunk in tile.uVarsMulti)
                    {
                        switch (roomchunk[2])
                        {
                            case 0:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address1);
                                break;
                            case 1:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address2);
                                break;
                            case 2:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address3);
                                break;
                            case 3:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address4);
                                break;
                            case 4:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address5);
                                break;
                            case 5:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address6);
                                break;
                            case 6:
                                _memory.SafeWrite((memoryAddress + (nuint)counter), address7);
                                break;
                            default:
                                throw new ToBeNamedException(_utils);
                        }
                        counter+=4;
                    }
                }
            }
            return memoryAddress;
        }


        [Function(Register.rbx, Register.rcx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDebugASMFunction(int ebx);
        */
    }
}
