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
using static p4gpc.dungeonloader.Accessors.RoomAccessors;
using static p4gpc.dungeonloader.Accessors.MinimapAccessors;

namespace p4gpc.dungeonloader.Accessors
{
    public class RoomCompareAccessors
    {

        private struct TileAddressLink
        {
            /*
             May have some misnomers here, but its the functionality that counts.
            
             Since each given tile can splinter into multiple tile images when concerning the minimap, the workaround here
             is to create a seperate table that tells where the start of any given minimap tile data is.
             For example, assuming the actual table is at 0x30000000, lookup for tile 3 would be 0x30000010, one above where it is numerically,
             because tile 2 has a door in it and therefore splits into two seperate images (one for each side of the door while it is unopened)
             */
            public nuint baseLookupAddress;
            public nuint baseActualAddress;
        }

        private IReloadedHooks? _hooks;
        private Utilities? _utils;
        private IMemory _memory;
        private Config _configuration;
        private JsonImporter _jsonImporter;
        private List<IReverseWrapper> _reverseWrapperList;
        private List<IAsmHook> _functionHookList;
        private List<DungeonRooms> _dungeonRooms;
        private List<DungeonMinimap> _minimap;
        private List<String> _commands;
        private int _minimap_image_count;

        public RoomCompareAccessors(IReloadedHooks hooks, Utilities utils, IMemory memory, Config config, JsonImporter jsonImporter)
        {
            _hooks = hooks;
            _utils = utils;
            _memory = memory;
            _configuration = config;
            _jsonImporter = jsonImporter;
            _reverseWrapperList = new List<IReverseWrapper>();
            _functionHookList = new List<IAsmHook>();
            _dungeonRooms = _jsonImporter.GetRooms();
            _minimap = _jsonImporter.GetMinimap();
            _commands = new List<String>();
            _minimap_image_count=0;
            for (int i = 0; i < _minimap.Count(); i++)
            {
                /*
                 Time to explain some shennanigans here, in the default game state, each multi-texture room
                 has a texture that shows the whole tile as a single piece. These are unused, but are still loaded
                 messed with on startup. As such, I'm including them for the moment until some more modifications
                 are made, so even multi-texture room minimaps will have a valid 'name' variable

                Keeping count for the moment, if it can be scrapped later it possibly will be to save space + write time.
                 */
                _minimap_image_count++;
                if (_minimap[i].multipleNames)
                {
                    for (int j = 0; j < _minimap[i].names.Count(); j++)
                    {
                        _minimap_image_count++;
                    }
                }
            }

            List<Task> initialTasks = new List<Task>();
            initialTasks.Add(Task.Run((() => Initialize())));
            Task.WaitAll(initialTasks.ToArray());
            _utils.Log("Room compare hooks established.");
        }

        private void Initialize()
        {
            Debugger.Launch();
            long address;
            List<String> functions = _jsonImporter.GetRoomCompareFunctions();

            IReverseWrapper<LogDebugASMFunction> reverseWrapperLogDebugASM = _hooks.CreateReverseWrapper<LogDebugASMFunction>(LogDebugASM);
            _commands.Add($"{_hooks.Utilities.GetAbsoluteCallMnemonics(LogDebugASM, out reverseWrapperLogDebugASM)}");
            _reverseWrapperList.Add(reverseWrapperLogDebugASM);


            /*
             Just to explain the out-of-order thing, Minimap_Adj_1 through Minimap_Adj_4 were written first, but it turns out 
             Minimap_Adj_5 needed to be written and jumps to an address replaced by Minimap_Adj_1. Plan is to rename these functions
             to something more useful when more information is obtained, but until such time execution will be like this.
             */
            address = _utils.SigScan(functions[4], "Minimap_Adj_5");
            Minimap_Adj_5((int)address, functions[4]);
            _utils.LogDebug($"Minimap_Adj_5 address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[0], "Minimap_Adj_1");
            Minimap_Adj_1((int)address, functions[0]);
            _utils.LogDebug($"Minimap_Adj_1 address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[1], "Minimap_Adj_2");
            Minimap_Adj_2((int)address, functions[1]);
            _utils.LogDebug($"Minimap_Adj_2 address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[2], "Minimap_Adj_3");
            Minimap_Adj_3((int)address, functions[2]);
            _utils.LogDebug($"Minimap_Adj_3 address: {address.ToString("X8")}");

            address = _utils.SigScan(functions[3], "Minimap_Adj_4");
            Minimap_Adj_4((int)address, functions[3]);
            _utils.LogDebug($"Minimap_Adj_4 address: {address.ToString("X8")}");

        }

        private void Minimap_Adj_1(int functionAddress, string pattern)
        {
            // Code to replace:
            // 83 C0 F7 83 F8 05 0F 87 FE 03 00 00

            List<string> instruction_list = new List<string>();
            nuint tableAddress = createMinimapAdjustTable1();
            instruction_list.Add($"use32");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"and eax, 0xFF");

            /*
            instruction_list.Add($"cmp eax, 0xF");
            instruction_list.Add($"je debug_log");
            instruction_list.Add($"label func_execution");
            */

            instruction_list.Add($"sub eax, 1");
            instruction_list.Add($"shl eax, 0x2");
            instruction_list.Add($"mov ebx, {tableAddress}");
            instruction_list.Add($"add ebx, eax");
            instruction_list.Add($"mov eax, [ebx]");
            instruction_list.Add($"mov [esp+0xC], eax");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");

            instruction_list.Add($"ret");

            /*
            //LogDebugASMFunction
            instruction_list.Add($"label debug_log");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ebx, 0x0");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"jmp func_execution");
            */
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void Minimap_Adj_2(int functionAddress, string pattern)
        {
            // Code to replace:
            // 80 FB 09 0F 82 D4 00 00 00

            List<string> instruction_list = new List<string>();
            nuint tableAddress = createMinimapAdjustTable3();
            nuint lookupAddress = createLookupTable(tableAddress);

            instruction_list.Add($"use32");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"and ebx, 0xFF");
            instruction_list.Add($"and ecx, 0xFF");

            /*
            instruction_list.Add($"cmp ebx, 0xF");
            instruction_list.Add($"je debug_log");
            instruction_list.Add($"label func_execution");
            */

            instruction_list.Add($"sub ebx, 1");
            instruction_list.Add($"sub ecx, 1");
            instruction_list.Add($"shl ebx, 0x2");
            instruction_list.Add($"shl ecx, 0x2");
            instruction_list.Add($"mov ebx, [{lookupAddress} + ebx]");
            instruction_list.Add($"add ebx, ecx");
            instruction_list.Add($"mov eax, [ebx]");
            instruction_list.Add($"mov [esp+0xC], eax");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"ret");

            /*
            //LogDebugASMFunction
            instruction_list.Add($"label debug_log");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ebx, 0x1");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"jmp func_execution");
            */
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void Minimap_Adj_3(int functionAddress, string pattern)
        {
            // Code to replace:
            // 3C 09 72 12 0F B6 C0 83 C0 F7
            nuint tableAddress = createMinimapAdjustTable2();

            List<string> instruction_list = new List<string>();
            instruction_list.Add($"use32");
            instruction_list.Add($"mov eax, [esp+0x33]");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");

            instruction_list.Add($"and eax, 0xFF");

            /*
            instruction_list.Add($"cmp eax, 0xF");
            instruction_list.Add($"je debug_log");
            instruction_list.Add($"label func_execution");
            */

            instruction_list.Add($"sub eax, 1");
            instruction_list.Add($"shl eax, 0x2");
            instruction_list.Add($"mov ebx, {tableAddress}");
            instruction_list.Add($"add ebx, eax");
            instruction_list.Add($"mov eax, [ebx]");
            instruction_list.Add($"mov [esp+0xC], eax");
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"ret");

            /*
            //LogDebugASMFunction
            instruction_list.Add($"label debug_log");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ebx, 0x2");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"jmp func_execution");
            */
            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void Minimap_Adj_4(int functionAddress, string pattern)
        {
            // Code to replace:
            // 3C 09 0F 82 8D 03 00 00 0F B6 C0 83 C0 F7 83 F8 05 0F 87 35 04 00 00

            List<string> instruction_list = new List<string>();
            nuint tableAddress = createMinimapAdjustTable4();
            instruction_list.Add($"use32");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");

            instruction_list.Add($"and eax, 0xFF");

            /*
            instruction_list.Add($"cmp eax, 0xF");
            instruction_list.Add($"je debug_log");
            instruction_list.Add($"label func_execution");
            */

            instruction_list.Add($"sub eax, 1");
            instruction_list.Add($"shl eax, 0x2");
            instruction_list.Add($"mov ebx, {tableAddress}");
            instruction_list.Add($"add ebx, eax");
            instruction_list.Add($"mov eax, [ebx]");
            instruction_list.Add($"mov [esp+0xC], eax");

            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");

            instruction_list.Add($"ret");

            /*
            //LogDebugASMFunction
            instruction_list.Add($"label debug_log");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ebx, 0x3");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"jmp func_execution");
            */


            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }

        private void Minimap_Adj_5(int functionAddress, string pattern)
        {
            // Code to replace
            // 3C 06 0F 87 CA 02 00 00 3C 02 0F 85 A5 02 00 00

            List<string> instruction_list = new List<string>();
            nuint tableAddress = createMinimapAdjustTable5();
            instruction_list.Add($"use32");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");

            instruction_list.Add($"and eax, 0xFF");

            /*
            instruction_list.Add($"cmp eax, 0xF");
            instruction_list.Add($"je debug_log");
            instruction_list.Add($"label func_execution");
            */

            instruction_list.Add($"sub eax, 1");
            instruction_list.Add($"shl eax, 0x2");
            instruction_list.Add($"mov ebx, {tableAddress}");
            instruction_list.Add($"add ebx, eax");
            instruction_list.Add($"mov eax, [ebx]");
            instruction_list.Add($"mov [esp+0xC], eax");

            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");

            instruction_list.Add($"ret");

            /*
            //LogDebugASMFunction
            instruction_list.Add($"label debug_log");
            instruction_list.Add($"push eax");
            instruction_list.Add($"push ebx");
            instruction_list.Add($"push ecx");
            instruction_list.Add($"mov ebx, 0x4");
            instruction_list.Add(_commands[0]);
            instruction_list.Add($"pop ecx");
            instruction_list.Add($"pop ebx");
            instruction_list.Add($"pop eax");
            instruction_list.Add($"jmp func_execution");
            */


            _functionHookList.Add(_hooks.CreateAsmHook(instruction_list.ToArray(), functionAddress, AsmHookBehaviour.DoNotExecuteOriginal, _utils.GetPatternLength(pattern)).Activate());
        }
        private nuint createMinimapAdjustTable1()
        {
            nuint memoryAddress;
            int address1 = (int)_utils.SigScan("43 83 C6 10 89 5C 24 14 83 FB 10 0F 8C 85 FB FF FF", "BranchDefault");
            int address2 = (int)_utils.SigScan("8A 87 21 AC DC 00 C0 E8 04 3C 01 0F 84 57 FE FF FF", "Branch1");
            int address3 = (int)_utils.SigScan("8A 8F 21 AC DC 00 88 C8 C0 E8 04 3C 01 0F 85 91 01 00 00", "Branch2");
            int address4 = (int)_utils.SigScan("8A 87 21 AC DC 00 C0 E8 04 3C 01 0F 84 29 FB FF FF", "Branch3");
            memoryAddress = _memory.Allocate(4 * _minimap.Count());
            int counter = 0;
            foreach (DungeonMinimap tile in _minimap)
            {
                switch (tile.uVarsSingle[0])
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
                    default:
                        throw new ToBeNamedExcpetion(_utils);
                }
                counter+=4;
            }
            return memoryAddress;
        }

        private nuint createMinimapAdjustTable2()
        {
            nuint memoryAddress;
            int address1 = (int)_utils.SigScan("C7 47 2C 00 00 98 41 C7 47 30 00 00 98 41 C7 47 1C 00 00 18 3F", "BranchDefault");
            int address2 = (int)_utils.SigScan("C6 87 A8 00 00 00 00 0F BE 44 24 2B 8B 7C 24 38 0F BE CA", "Branch1");
            int address3 = (int)_utils.SigScan("C7 47 24 00 00 80 3C C7 47 28 00 00 80 3C C7 47 20 00 00 14 3F", "Branch2");
            memoryAddress = _memory.Allocate(4 * _minimap.Count());
            int counter = 0;
            foreach (DungeonMinimap tile in _minimap)
            {
                switch (tile.uVarsSingle[1])
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
                    default:
                        throw new ToBeNamedExcpetion(_utils);
                }
                counter+=4;
            }
            return memoryAddress;
        }

        private nuint createMinimapAdjustTable3()
        {
            nuint memoryAddress;
            int address1 = (int)_utils.SigScan("C7 46 24 00 00 00 3D C7 46 28 00 00 00 3D", "BranchDefault");
            int address2 = (int)_utils.SigScan("C7 46 24 00 00 80 3C C7 46 28 00 00 80 3C C7 46 1C 00 00 98 3E C7 46 20 00 00 14 3F 38 CA", "Branch1");
            int address3 = (int)_utils.SigScan("C7 46 20 00 00 14 3F C7 46 1C 00 00 14 3F C7 46 30 00 00 98 41", "Branch2");
            int address4 = (int)_utils.SigScan("C7 46 24 00 00 80 3C C7 46 28 00 00 80 3C C7 46 20 00 00 14 3F", "Branch3");
            int address5 = (int)_utils.SigScan("C7 46 24 00 00 98 3E C7 46 28 00 00 80 3C C7 46 1C 00 00 60 3F", "Branch4");
            int address6 = (int)_utils.SigScan("C7 46 28 00 00 80 3C C7 46 24 00 00 80 3C C7 46 1C 00 00 98 3E", "Branch5");
            int address7 = (int)_utils.SigScan("C7 46 1C 00 00 5C 3F C7 46 20 00 00 5C 3F C7 46 30 00 00 14 42", "Branch6");
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
                        throw new ToBeNamedExcpetion(_utils);
                }
                counter+=4;
                if (tile.multipleNames)
                {
                    foreach(List<byte> roomchunk in tile.uVarsMulti)
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
                                throw new ToBeNamedExcpetion(_utils);
                        }
                        counter+=4;
                    }
                }
            }
            return memoryAddress;
        }

        private nuint createMinimapAdjustTable4()
        {
            nuint memoryAddress;
            int address1 = (int)_utils.SigScan("8A 06 88 D1 51 88 DA 89 44 24 1C", "Is3x3_exitDefault");
            int address2 = (int)_utils.SigScan("8A 0E 0F B6 C1 01 C0 8A B8 30 AD A5 00", "Is3x3_exit1");
            int address3 = (int)_utils.SigScan("8A 0E 0F B6 C1 01 C0 8A B8 50 AD A5 00", "Is3x3_exit2");
            int address4 = (int)_utils.SigScan("8A 0E 0F B6 C1 01 C0 8A B8 70 AD A5 00", "Is3x3_exit3");
            memoryAddress = _memory.Allocate(4 * _minimap.Count());
            int counter = 0;
            foreach (DungeonMinimap tile in _minimap)
            {
                switch (tile.uVarsSingle[3])
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
                    default:
                        throw new ToBeNamedExcpetion(_utils);
                }
                counter+=4;
            }
            return memoryAddress;
        }

        private nuint createMinimapAdjustTable5()
        {
            nuint memoryAddress;
            int address1 = (int)_utils.SigScan("A1 B8 A8 E0 04 66 8B 0C 70 8D 14 70", "adjust5_Default");
            int address2 = (int)_utils.SigScan("8B 15 B8 A8 E0 04 0F B6 CF 8D 1C 72 89", "adjust5_1");
            int address3 = (int)_utils.SigScan("0F B6 C0 83 C0 F7 83 F8 05 0F 87 FE 03 00 00", "adjust5_2");
            int address4 = (int)_utils.SigScan("8A 8F 21 AC DC 00 88 C8 C0 E8 04 3C 01 0F 85 8A 02 00 00", "adjust5_3");
            memoryAddress = _memory.Allocate(4 * _minimap.Count());
            int counter = 0;
            foreach (DungeonMinimap tile in _minimap)
            {
                switch (tile.uVarsSingle[4])
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
                    default:
                        throw new ToBeNamedExcpetion(_utils);
                }
                counter+=4;
            }
            return memoryAddress;
        }


        private nuint createLookupTable(nuint actualTable)
        {
            nuint lookupAddress;
            int lookupCounter = 0;
            int actualCounter = 0;
            lookupAddress = _memory.Allocate(4 *_minimap.Count());
            foreach (DungeonMinimap tile in _minimap)
            {
                if (tile.multipleNames)
                {
                    _memory.SafeWrite((lookupAddress + (nuint)lookupCounter), (actualTable + (nuint)(actualCounter+4)));
                }
                else
                {
                    _memory.SafeWrite((lookupAddress + (nuint)lookupCounter), (actualTable + (nuint)(actualCounter)));

                }

                lookupCounter+=4;
                actualCounter+=4;
                if (tile.multipleNames)
                {
                    foreach (List<byte> roomchunk in tile.uVarsMulti)
                    {
                        actualCounter+=4;
                    }
                }
                
            }
            return (nuint)lookupAddress;
        }

        private void LogDebugASM(int addressmsg)
        {
            string msg;
            switch (addressmsg)
            {
                case 0:
                    msg = "Minimap_Adj_1";
                    break;
                case 1:
                    msg = "Minimap_Adj_2";
                    break;
                case 2:
                    msg = "Minimap_Adj_3";
                    break;
                case 3:
                    msg = "Minimap_Adj_4";
                    break;
                case 4:
                    msg = "Minimap_Adj_5";
                    break;
                default:
                    throw new ToBeNamedExcpetion(_utils);
            }
            _utils.LogDebug("Hit function " + msg);
        }

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int BranchingLargeJumpsFunction(int eax);

        [Function(new[] { Register.ebx, Register.ecx, Register.edx, Register.esi }, Register.ebx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void UnknownRoomWritesFunction(int ebx, int ecx, int edx, int esi);

        [Function(new[] { Register.eax, Register.edi }, Register.eax, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void UnknownRoomWrites2Function(int eax, int edi);

        [Function(Register.eax, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int Is3x3Function(int eax);

        [Function(Register.ebx, Register.ecx, StackCleanup.Callee)]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LogDebugASMFunction(int ebx);

    }
}
